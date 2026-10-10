using System.Security.Cryptography;
using System.Text;
using Goro.Hashing;
using Goro.Reading.Bytes;
using Goro.Reading.Flac;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Hashing;

public class AudioHasherTests
{
    private DirectoryInfo _tempDir = null!;
    private AudioHasher _hasher = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Directory.CreateTempSubdirectory("goro-hash-tests-");
        _hasher = new AudioHasher(ReadPolicy.Default);
    }

    [TearDown]
    public void TearDown() => _tempDir.Delete(recursive: true);

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir.FullName, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Test]
    public async Task ComputeHashAsync_SameAudioDifferentTags_ProducesSameHash()
    {
        var audio = SyntheticMp3Builder.BuildAudioFrames();
        var fileA = WriteFile("a.mp3", SyntheticMp3Builder.BuildMp3(audio, SyntheticMp3Builder.BuildId3V2(100), SyntheticMp3Builder.BuildId3V1()));
        var fileB = WriteFile("b.mp3", SyntheticMp3Builder.BuildMp3(audio, SyntheticMp3Builder.BuildId3V2(50, fill: 0x58)));

        var hashA = await _hasher.ComputeHashAsync(fileA, HashAlgorithmKind.Md5, CancellationToken.None);
        var hashB = await _hasher.ComputeHashAsync(fileB, HashAlgorithmKind.Md5, CancellationToken.None);

        Assert.That(hashA.Hex, Is.EqualTo(hashB.Hex));
    }

    [Test]
    public async Task ComputeHashAsync_DifferentAudioSameTags_ProducesDifferentHash()
    {
        var audio = SyntheticMp3Builder.BuildAudioFrames();
        var mutatedAudio = (byte[])audio.Clone();
        mutatedAudio[4 + 200] ^= 0xFF; // flip a byte inside a frame body, well past its header

        var tag = SyntheticMp3Builder.BuildId3V2(100);
        var fileA = WriteFile("a.mp3", SyntheticMp3Builder.BuildMp3(audio, tag));
        var fileB = WriteFile("b.mp3", SyntheticMp3Builder.BuildMp3(mutatedAudio, tag));

        var hashA = await _hasher.ComputeHashAsync(fileA, HashAlgorithmKind.Md5, CancellationToken.None);
        var hashB = await _hasher.ComputeHashAsync(fileB, HashAlgorithmKind.Md5, CancellationToken.None);

        Assert.That(hashA.Hex, Is.Not.EqualTo(hashB.Hex));
    }

    [Test]
    public async Task ComputeHashAsync_UsesRequestedAlgorithm()
    {
        var audio = SyntheticMp3Builder.BuildAudioFrames();
        var file = WriteFile("a.mp3", SyntheticMp3Builder.BuildMp3(audio, SyntheticMp3Builder.BuildId3V2(10)));

        var md5 = await _hasher.ComputeHashAsync(file, HashAlgorithmKind.Md5, CancellationToken.None);
        var sha1 = await _hasher.ComputeHashAsync(file, HashAlgorithmKind.Sha1, CancellationToken.None);

        var expectedMd5 = Convert.ToHexString(MD5.HashData(audio)).ToLowerInvariant();
        var expectedSha1 = Convert.ToHexString(SHA1.HashData(audio)).ToLowerInvariant();

        Assert.That(md5.Hex, Is.EqualTo(expectedMd5), "frames without a summary header, and nothing after them: the hash covers all of them");
        Assert.That(sha1.Hex, Is.EqualTo(expectedSha1));
    }

    [Test]
    public void ComputeHashAsync_UnparseableFile_ThrowsRatherThanReturningAWrongHash()
    {
        var file = WriteFile("not-audio.mp3", Encoding.UTF8.GetBytes("this is not a valid mpeg stream at all"));

        Assert.That(() => _hasher.ComputeHashAsync(file, HashAlgorithmKind.Md5, CancellationToken.None),
            Throws.TypeOf<InvalidDataException>().With.Message.EqualTo("no MPEG audio found"));
    }

    [Test]
    public async Task ComputeHashAsync_DamagedTag_HashesAsTheHealthyFile_AndSaysPartOfItCouldNotBeRead()
    {
        var damaged = await _hasher.ComputeHashAsync(AudioCorpus.PathOf("mp3/dmg-id3v24-frame-overrun.mp3"), HashAlgorithmKind.Md5, CancellationToken.None);
        var healthy = await _hasher.ComputeHashAsync(AudioCorpus.PathOf("mp3/id3v24.mp3"), HashAlgorithmKind.Md5, CancellationToken.None);

        Assert.That(damaged.Hex, Is.EqualTo(healthy.Hex));
        Assert.That(damaged.Incomplete, Is.True);
        Assert.That(healthy.Incomplete, Is.False);
    }

    // --- FLAC ---

    private static readonly string[] DamagedAudio = ["flac/truncated.flac", "flac/zeroed-4k.flac", "flac/last-frame-damaged.flac"];

    private async Task<AudioHash> Hash(string path) => await _hasher.ComputeHashAsync(path, HashAlgorithmKind.Sha1, CancellationToken.None);

    /// <summary>
    /// Every FLAC file in the corpus holds the same frames, the seeds' own, under different metadata,
    /// stray tags, junk and damage to its tags: all of them hash alike but the three whose audio is
    /// damaged, which hash unlike the rest and unlike one another.
    /// </summary>
    [Test]
    public async Task EveryFlacFileWithTheSameFrames_HashesAlike_WhateverItsMetadataAndTags()
    {
        var hashes = new Dictionary<string, string>();
        foreach (var file in AudioCorpus.FlacFiles)
        {
            hashes[file] = (await Hash(AudioCorpus.PathOf(file))).Hex;
        }

        var healthy = hashes["seeds/flac.flac"];
        Assert.Multiple(() =>
        {
            foreach (var (file, hex) in hashes.Where(h => !DamagedAudio.Contains(h.Key)))
            {
                Assert.That(hex, Is.EqualTo(healthy), file);
            }

            Assert.That(DamagedAudio.Select(file => hashes[file]).Append(healthy).Distinct().Count(), Is.EqualTo(DamagedAudio.Length + 1));
        });
    }

    [Test]
    public async Task EditingAFlacFilesMetadata_LeavesItsHashAlone()
    {
        var bytes = AudioCorpus.Bytes("flac/comments.flac");
        var reader = new BoundedReader(new MemoryByteSource(bytes), ReadPolicy.Default);
        var metadata = FlacMetadata.Read(reader, 4);
        var blocks = metadata.Blocks.Select(b => (Type: (int)b.Type, Payload: bytes[(int)b.Payload.Start..(int)b.Payload.End])).ToList();
        var frames = bytes[(int)metadata.End!.Value..];

        var withoutPadding = Build(blocks.Where(b => b.Type != (int)FlacBlockType.Padding), frames);
        var reordered = Build([blocks[0], .. blocks.Skip(1).Reverse()], frames);
        var grown = Build([.. blocks, ((int)FlacBlockType.Application, new byte[20_000])], frames);

        var original = (await Hash(WriteFile("original.flac", bytes))).Hex;
        foreach (var (name, edited) in new[] { ("no-padding.flac", withoutPadding), ("reordered.flac", reordered), ("grown.flac", grown) })
        {
            Assert.That((await Hash(WriteFile(name, edited))).Hex, Is.EqualTo(original), name);
        }

        Assert.That(grown.Length, Is.GreaterThan(bytes.Length), "the frames moved");
    }

    [Test]
    public async Task AFlippedBitInAFlacFrame_ChangesTheHash()
    {
        var bytes = AudioCorpus.Bytes("flac/comments.flac");
        var original = (await Hash(WriteFile("original.flac", bytes))).Hex;
        bytes[30_000] ^= 0x01;

        Assert.That((await Hash(WriteFile("flipped.flac", bytes))).Hex, Is.Not.EqualTo(original));
    }

    [TestCase("flac/dmg-comment-length-overrun.flac", true)]
    [TestCase("flac/dmg-application-length-overrun.flac", true)]
    [TestCase("flac/rw-comment-block-too-short.flac", false)]
    [TestCase("flac/comments.flac", false)]
    public async Task AFlacFileWithDamagedTags_SaysSo(string file, bool incomplete)
    {
        Assert.That((await Hash(AudioCorpus.PathOf(file))).Incomplete, Is.EqualTo(incomplete));
    }

    [Test]
    public void AFlacFileWithNoAudio_HasNoHash()
    {
        var file = WriteFile("not-audio.flac", Encoding.UTF8.GetBytes("this is not a FLAC stream at all"));

        Assert.That(() => _hasher.ComputeHashAsync(file, HashAlgorithmKind.Md5, CancellationToken.None),
            Throws.TypeOf<InvalidDataException>().With.Message.EqualTo("no FLAC audio found"));
    }

    /// <summary>A FLAC file of <paramref name="blocks"/>, the last flagged, then <paramref name="frames"/>.</summary>
    private static byte[] Build(IEnumerable<(int Type, byte[] Payload)> blocks, byte[] frames)
    {
        var list = blocks.ToList();
        var bytes = new List<byte>("fLaC"u8.ToArray());
        for (var i = 0; i < list.Count; i++)
        {
            var (type, payload) = list[i];
            bytes.Add((byte)((i == list.Count - 1 ? 0x80 : 0) | type));
            bytes.AddRange([(byte)(payload.Length >> 16), (byte)(payload.Length >> 8), (byte)payload.Length]);
            bytes.AddRange(payload);
        }

        return [.. bytes, .. frames];
    }
}
