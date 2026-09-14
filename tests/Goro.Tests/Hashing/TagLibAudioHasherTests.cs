using System.Security.Cryptography;
using System.Text;
using Goro.Hashing;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Hashing;

public class TagLibAudioHasherTests
{
    private DirectoryInfo _tempDir = null!;
    private TagLibAudioHasher _hasher = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Directory.CreateTempSubdirectory("goro-hash-tests-");
        _hasher = new TagLibAudioHasher();
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

        Assert.That(hashA, Is.EqualTo(hashB));
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

        Assert.That(hashA, Is.Not.EqualTo(hashB));
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

        Assert.That(md5, Is.EqualTo(expectedMd5));
        Assert.That(sha1, Is.EqualTo(expectedSha1));
        Assert.That(md5, Is.Not.EqualTo(sha1));
    }

    [Test]
    public void ComputeHashAsync_UnparseableFile_ThrowsRatherThanReturningAWrongHash()
    {
        var file = WriteFile("not-audio.mp3", Encoding.UTF8.GetBytes("this is not a valid mpeg stream at all"));

        Assert.ThrowsAsync<TagLib.CorruptFileException>(() =>
            _hasher.ComputeHashAsync(file, HashAlgorithmKind.Md5, CancellationToken.None));
    }
}
