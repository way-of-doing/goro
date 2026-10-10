using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Flac;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// The range <c>goro hash</c> hashes in a FLAC file, <c>[D, E)</c> of docs/design/flac-layout.md,
/// and the checksum failures the walk to find it records (docs/implementation.md, "Finding where a
/// FLAC file's audio ends"). The positions expected are the ones flac-layout measured.
/// </summary>
public class FlacAudioRangeTests
{
    private static FlacAudioWalk Walk(byte[] bytes)
    {
        var source = new MemoryByteSource(bytes);
        return FlacAudioRange.Find(source, FlacAnalysis.Analyse(new BoundedReader(source, ReadPolicy.Default)));
    }

    private static FlacAudioWalk Walk(string file) => Walk(AudioCorpus.Bytes(file));

    [TestCase("seeds/flac.flac", 8304, 50411)]
    [TestCase("flac/comments.flac", 4496, 46603)]
    [TestCase("flac/id3v2-prefix.flac", 4610, 46717)]
    [TestCase("flac/id3v1-suffix.flac", 4496, 46603)]
    [TestCase("flac/ape-suffix.flac", 4496, 46603)]
    [TestCase("flac/garbage-appended.flac", 4496, 46603)]
    [TestCase("flac/dmg-id3v2-size-short.flac", 5100, 47207)]
    [TestCase("flac/dmg-application-length-overrun.flac", 1402, 43509)]
    public void TheRange_RunsFromTheFirstFrameToTheEndOfTheLast_AndAHealthyStreamHasNoFaults(string file, long start, long end)
    {
        var walk = Walk(file);
        Assert.That(walk.Range, Is.EqualTo(Region.Between(start, end)));
        Assert.That(walk.Faults, Is.Empty);
    }

    [Test]
    public void AFrameCutOff_IsLeftOut_AndRecorded()
    {
        var walk = Walk("flac/truncated.flac");
        Assert.That(walk.Range, Is.EqualTo(Region.Between(4496, 26868)));
        Assert.That(walk.Faults, Is.EqualTo(new[] { new FlacFrameFault(FlacFrameFaultKind.CutOff, 26868, 19) }));
    }

    [Test]
    public void ADamagedLastHeader_IsRecorded_AndItsFrameLeftOut()
    {
        var walk = Walk("flac/last-frame-damaged.flac");
        Assert.That(walk.Range, Is.EqualTo(Region.Between(4496, 45961)));
        // The fixture's damage flips a bit of the header's frame number, so it records 34 where 35 was due.
        Assert.That(walk.Faults, Is.EqualTo(new[] { new FlacFrameFault(FlacFrameFaultKind.HeaderCrcFails, 45961, 34) }));
    }

    [Test]
    public void DamageInTheMiddle_StaysInsideTheRange_AndIsRecorded()
    {
        var walk = Walk("flac/zeroed-4k.flac");

        Assert.That(walk.Range, Is.EqualTo(Region.Between(4496, 46603)));
        Assert.That(walk.Faults.Select(f => f.Kind), Does.Contain(FlacFrameFaultKind.Crc16Fails));
        Assert.That(walk.Faults.Select(f => f.Kind), Does.Contain(FlacFrameFaultKind.NumbersSkipped));
    }

    [Test]
    public void AFlippedBit_FailsItsFramesCrc16()
    {
        var bytes = AudioCorpus.Bytes("flac/comments.flac");
        bytes[20_000] ^= 0x04;

        var walk = Walk(bytes);

        Assert.That(walk.Range, Is.EqualTo(Region.Between(4496, 46603)));
        Assert.That(walk.Faults, Has.Exactly(1).Property(nameof(FlacFrameFault.Kind)).EqualTo(FlacFrameFaultKind.Crc16Fails));
    }

    [Test]
    public void AStreamOfVariableBlockSize_IsWalkedByItsSampleNumbers()
    {
        int[] sizes = [1000, 3000, 2000, 4608, 17];
        var walk = Walk(SyntheticFlacBuilder.Stream((ulong)sizes.Sum(), sizes, variable: true));

        Assert.That(walk.Faults, Is.Empty);
        Assert.That(walk.Range.End, Is.EqualTo(SyntheticFlacBuilder.Stream((ulong)sizes.Sum(), sizes, variable: true).Length));
    }

    [Test]
    public void ALongStream_IsWalkedToItsEnd()
    {
        var sizes = Enumerable.Repeat(4096, 20_000).Append(123).ToArray();
        var bytes = SyntheticFlacBuilder.Stream(4096UL * 20_000 + 123, sizes);

        var walk = Walk(bytes);

        Assert.That(walk.Faults, Is.Empty);
        Assert.That(walk.Range.End, Is.EqualTo(bytes.Length));
    }
}
