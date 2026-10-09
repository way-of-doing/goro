using System.Security.Cryptography;
using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// The hash range, for MP3 (docs/testing.md, "Reading audio files"): one hash for the same audio
/// whatever surrounds it, and a different one when the audio itself changes. This is the property the
/// hash exists for, so it is asserted over every fixture built from the same seed.
/// </summary>
public class HashRangeTests
{
    /// <summary>The MD5 of the range <see cref="Mp3AudioRange"/> finds, worked out here from the bytes themselves.</summary>
    internal static string Hash(byte[] file)
    {
        var source = new MemoryByteSource(file);
        var layout = Mp3Analysis.Analyse(new BoundedReader(source, ReadPolicy.Default));
        var range = Mp3AudioRange.Find(source, layout);
        return Convert.ToHexStringLower(MD5.HashData(file.AsSpan((int)range.Start, (int)range.Length)));
    }

    private static string Hash(string file) => Hash(AudioCorpus.Bytes(file));

    private static IEnumerable<TestCaseData> BuiltFrom(string group, string? except = null) =>
        AudioCorpus.Manifest
            .Where(entry => entry.Group == group && entry.File.StartsWith("mp3/", StringComparison.Ordinal) && entry.File != except)
            .Select(entry => new TestCaseData(entry.File).SetName($"one hash: {entry.File}"));

    private static IEnumerable<TestCaseData> FromTheCbrSeed() =>
        BuiltFrom("layout").Concat(BuiltFrom("tag-damage", except: "mp3/dmg-id3v24-truncated-in-tag.mp3"))
            .Append(new TestCaseData("mp3/garbage-prepended.mp3").SetName("one hash: mp3/garbage-prepended.mp3"))
            .Append(new TestCaseData("mp3/garbage-appended.mp3").SetName("one hash: mp3/garbage-appended.mp3"));

    // Every tag layout, every damaged tag, junk on either side, a Lyrics3 block, an APE tag at the
    // start: the audio of seeds/mp3-cbr.mp3, and so its hash.
    [TestCaseSource(nameof(FromTheCbrSeed))]
    public void EveryFixtureBuiltFromTheCbrSeed_HashesAsTheSeedDoes(string file)
    {
        Assert.That(Hash(file), Is.EqualTo(Hash("seeds/mp3-cbr.mp3")));
    }

    private static IEnumerable<TestCaseData> FromTheHeaderlessSeed() => BuiltFrom("real-world", except: "mp3/rw-cbr-no-info-truncated.mp3");

    [TestCaseSource(nameof(FromTheHeaderlessSeed))]
    public void EveryRealWorldFixture_HashesAsItsHeaderlessSeedDoes(string file)
    {
        Assert.That(Hash(file), Is.EqualTo(Hash("seeds/mp3-cbr-notag.mp3")));
    }

    [Test]
    public void TheGaplessCountsAndTheInfoFrame_BelongToTheContainer_AndAreNotHashed()
    {
        var file = AudioCorpus.Bytes("seeds/mp3-cbr.mp3");
        var layout = AudioCorpus.Analyse(file).Layout;
        var lame = FindLameTag(file);
        var edited = (byte[])file.Clone();
        edited[lame + 21] ^= 0x10; // the encoder delay
        edited[lame + 23] ^= 0x01; // the padding
        edited[lame - 8] ^= 0x01;  // a byte of the seek table

        Assert.That(layout.Summary, Is.Not.Null);
        Assert.That(Hash(edited), Is.EqualTo(Hash(file)));
    }

    [Test]
    public void ABitFlippedInsideAnAudioFrame_ChangesTheHash()
    {
        var file = AudioCorpus.Bytes("seeds/mp3-cbr.mp3");
        var edited = (byte[])file.Clone();
        edited[file.Length / 2] ^= 0x01;

        Assert.That(Hash(edited), Is.Not.EqualTo(Hash(file)));
    }

    [Test]
    public void DifferentAudio_HashesDifferently()
    {
        Assert.That(Hash("seeds/mp3-vbr.mp3"), Is.Not.EqualTo(Hash("seeds/mp3-cbr.mp3")));
        Assert.That(Hash("mp3/vbr-bitflips.mp3"), Is.Not.EqualTo(Hash("seeds/mp3-vbr.mp3")));
    }

    private static int FindLameTag(byte[] file)
    {
        var at = file.AsSpan().IndexOf("LAME"u8);
        Assert.That(at, Is.GreaterThan(0), "the seed holds a LAME tag");
        return at;
    }
}
