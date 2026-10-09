using System.Security.Cryptography;
using System.Text;
using Goro.Hashing;
using Goro.Reading.Bytes;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Hashing;

public class Mp3AudioHasherTests
{
    private DirectoryInfo _tempDir = null!;
    private Mp3AudioHasher _hasher = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Directory.CreateTempSubdirectory("goro-hash-tests-");
        _hasher = new Mp3AudioHasher(ReadPolicy.Default);
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
}
