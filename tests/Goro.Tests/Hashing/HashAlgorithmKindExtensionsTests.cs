using System.Security.Cryptography;
using Goro.Hashing;

namespace Goro.Tests.Hashing;

public class HashAlgorithmKindExtensionsTests
{
    [TestCase("md5", HashAlgorithmKind.Md5)]
    [TestCase("MD5", HashAlgorithmKind.Md5)]
    [TestCase("sha1", HashAlgorithmKind.Sha1)]
    [TestCase("SHA1", HashAlgorithmKind.Sha1)]
    public void TryParse_KnownNamesCaseInsensitive_ReturnsExpectedValue(string value, HashAlgorithmKind expected)
    {
        var success = HashAlgorithmKindExtensions.TryParse(value, out var algorithm);

        Assert.That(success, Is.True);
        Assert.That(algorithm, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void TryParse_NullOrEmpty_DefaultsToMd5(string? value)
    {
        var success = HashAlgorithmKindExtensions.TryParse(value, out var algorithm);

        Assert.That(success, Is.True);
        Assert.That(algorithm, Is.EqualTo(HashAlgorithmKind.Md5));
    }

    [Test]
    public void TryParse_UnknownValue_ReturnsFalse()
    {
        var success = HashAlgorithmKindExtensions.TryParse("md6", out _);

        Assert.That(success, Is.False);
    }

    [Test]
    public void ValidNames_ListsExactlyTheSupportedAlgorithms()
    {
        Assert.That(HashAlgorithmKindExtensions.ValidNames, Is.EqualTo(new[] { "md5", "sha1" }));
    }

    [TestCase(HashAlgorithmKind.Md5, "md5")]
    [TestCase(HashAlgorithmKind.Sha1, "sha1")]
    public void ToName_RoundTripsTheParsedName(HashAlgorithmKind algorithm, string expectedName)
    {
        Assert.That(algorithm.ToName(), Is.EqualTo(expectedName));
    }

    [Test]
    public void CreateHashAlgorithm_Md5_ReturnsMd5Instance()
    {
        using var instance = HashAlgorithmKind.Md5.CreateHashAlgorithm();

        Assert.That(instance, Is.InstanceOf<MD5>());
    }

    [Test]
    public void CreateHashAlgorithm_Sha1_ReturnsSha1Instance()
    {
        using var instance = HashAlgorithmKind.Sha1.CreateHashAlgorithm();

        Assert.That(instance, Is.InstanceOf<SHA1>());
    }
}
