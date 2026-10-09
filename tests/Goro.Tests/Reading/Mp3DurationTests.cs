using Goro.Reading;
using Goro.Reading.Mp3;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// Playing time, for MP3 (docs/testing.md, "Reading audio files"): the length of the stream's
/// timeline, from a summary header that can be trusted or, without one, from the edges of a
/// constant-bitrate stream, and unusable otherwise. The expected lengths are the seeds' as ffmpeg
/// decodes them (the corpus README), and where ffmpeg is wrong, as docs/design/file-reading.md says.
/// </summary>
public class Mp3DurationTests
{
    private static DurationOutcome Duration(string file) => AudioCorpus.Analyse(file).Layout.Duration;

    private static void AssertKnown(string file, double seconds, DurationBasis basis)
    {
        var duration = Duration(file);
        Assert.That(duration, Is.InstanceOf<DurationOutcome.Known>(), file);
        var known = (DurationOutcome.Known)duration;
        Assert.That((double)known.Samples / known.SampleRate, Is.EqualTo(seconds).Within(0.0005), file);
        Assert.That(known.Basis, Is.EqualTo(basis), file);
    }

    private static void AssertUnusable(string file, DurationProblem problem)
    {
        var layout = AudioCorpus.Analyse(file).Layout;
        Assert.That(layout.Duration, Is.EqualTo(new DurationOutcome.Unusable(problem)), file);
        Assert.That(layout.Conditions.OfType<DurationUnusable>().Single().Problem, Is.EqualTo(problem));
        Assert.That(layout.IsIncomplete, Is.True, "a file whose playing time cannot be had is reported");
    }

    // 3.300 s and not the 3.344 s of 128 frames: the LAME tag's delay and padding are removed.
    [TestCase("seeds/mp3-cbr.mp3")]
    [TestCase("seeds/mp3-cbr-crc.mp3")]
    [TestCase("seeds/mp3-vbr.mp3")]
    [TestCase("seeds/mp3-vbr-stereo.mp3")]
    [TestCase("seeds/mp3-mpeg2-vbr.mp3")]
    [TestCase("seeds/mp3-freeformat.mp3")]
    public void ASummaryHeaderWithALameTag_GivesTheTrimmedTimeline(string file)
    {
        AssertKnown(file, 3.300, DurationBasis.SummaryHeader);
        Assert.That(AudioCorpus.Analyse(file).Layout.Summary?.Lame, Is.Not.Null);
    }

    [Test]
    public void TheTimeline_IsExact_InSamples()
    {
        Assert.That(((DurationOutcome.Known)Duration("seeds/mp3-cbr.mp3")).Samples, Is.EqualTo(145_530));
        AssertKnown("seeds/mp3-cbr-b.mp3", 1.700, DurationBasis.SummaryHeader);
    }

    [Test]
    public void AVbriHeader_GivesItsFrameCount()
    {
        AssertKnown("mp3/vbri.mp3", 3.344, DurationBasis.SummaryHeader);
        Assert.That(AudioCorpus.Analyse("mp3/vbri.mp3").Layout.Summary?.Kind, Is.EqualTo(SummaryKind.Vbri));
    }

    // ffmpeg misses the LAME tag in all of these and decodes the Info frame as audio, giving 3.370 s.
    [TestCase("mp3/garbage-prepended.mp3")]
    [TestCase("mp3/id3v24-junk-before-audio.mp3")]
    [TestCase("mp3/ape2-at-start.mp3")]
    [TestCase("mp3/dmg-id3v2-size-short.mp3")]
    [TestCase("mp3/dmg-id3v2-size-not-syncsafe.mp3")]
    public void AFirstFrameNotRightAfterTheTag_StillHasItsLameTagRead(string file)
    {
        AssertKnown(file, 3.300, DurationBasis.SummaryHeader);
    }

    // Damage in the middle does not shorten the timeline.
    [TestCase("mp3/vbr-bitflips.mp3")]
    [TestCase("mp3/vbr-zeroed-4k.mp3")]
    public void DamageInTheMiddle_LeavesTheNominalTimeline(string file)
    {
        AssertKnown(file, 3.300, DurationBasis.SummaryHeader);
    }

    [Test]
    public void AFileCutPartWayThroughItsLastFrame_IsWithinAFrameOfItsHeader()
    {
        AssertKnown("mp3/cbr-truncated-mid-frame.mp3", 3.300, DurationBasis.SummaryHeader);
    }

    [Test]
    public void JunkAppended_WhereAFrameEndsAsTheHeaderSays_IsTrustedToThatEnd_AndReported()
    {
        AssertKnown("mp3/garbage-appended.mp3", 3.300, DurationBasis.SummaryHeaderToFrameEnd);
        Assert.That(AudioCorpus.Analyse("mp3/garbage-appended.mp3").Layout.Conditions.OfType<BytesAfterAudio>(), Has.Exactly(1).Items);
    }

    // Counted from the edges: 128 frames of 1152 samples, with nothing recorded to trim.
    [TestCase("seeds/mp3-cbr-notag.mp3", 3.344)]
    [TestCase("seeds/mp3-mpeg25-cbr.mp3", 3.456)]
    [TestCase("mp3/rw-cbr-no-info.mp3", 3.344)]
    [TestCase("mp3/rw-apple-text-frames.mp3", 3.344)]
    public void AConstantBitrateFileWithoutAHeader_IsCountedFromItsEdges(string file, double seconds)
    {
        AssertKnown(file, seconds, DurationBasis.ConstantBitrateFromEdges);
    }

    [Test]
    public void ADoubledFrameCount_FailsTheLameCrc() => AssertUnusable("mp3/vbr-xing-count-doubled.mp3", DurationProblem.SummaryCrcFails);

    [Test]
    public void AFileCutInHalf_DisagreesWithItsHeader() => AssertUnusable("mp3/vbr-truncated-half.mp3", DurationProblem.SummaryDisagrees);

    [Test]
    public void TwoFilesJoined_DisagreeWithTheFirstHeader() => AssertUnusable("mp3/joined-cbr.mp3", DurationProblem.SummaryDisagrees);

    [TestCase("seeds/mp3-vbr-notag.mp3")]
    [TestCase("mp3/vbr-notag-id3v24.mp3")]
    [TestCase("mp3/vbr-xing-zeroed.mp3")]
    public void AVariableBitrateFileWithoutAHeader_IsUnusable_NotEstimated(string file) =>
        AssertUnusable(file, DurationProblem.NotConstantBitrate);

    [Test]
    public void AConstantBitrateFileWithoutAHeader_CutMidFrame_IsUnusable() =>
        AssertUnusable("mp3/rw-cbr-no-info-truncated.mp3", DurationProblem.NotConstantBitrate);

    [Test]
    public void WholeSeconds_AreTruncated_NotRounded()
    {
        Assert.That(new DurationOutcome.Known(39 * 4410, 44100, DurationBasis.SummaryHeader).WholeSeconds, Is.EqualTo(3), "3.9 s");
        Assert.That(new DurationOutcome.Known(44099, 44100, DurationBasis.SummaryHeader).WholeSeconds, Is.EqualTo(0));
    }
}
