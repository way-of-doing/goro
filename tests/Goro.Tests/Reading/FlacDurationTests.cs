using Goro.Reading;
using Goro.Tests.TestSupport;
using static Goro.Tests.TestSupport.SyntheticFlacBuilder;

namespace Goro.Tests.Reading;

/// <summary>
/// Playing time, for FLAC (docs/testing.md, the FLAC table; docs/implementation.md, "A FLAC file's
/// playing time is settled by the end of its audio"): STREAMINFO's total where the last frame
/// confirms it; the last frame's end where a short final block shows a natural end, or the frame
/// ends where the audio does; the start of a frame cut off by the end of the file; and unusable
/// where bytes that hold no frame follow the last one that can be read. The corpus's FLAC files
/// are all 3.300 s as ffmpeg decodes them, 145,530 samples at 44.1 kHz.
/// </summary>
public class FlacDurationTests
{
    private const long Whole = 145_530;

    private static void AssertKnown(string file, long samples, DurationBasis basis)
    {
        var layout = AudioCorpus.Analyse(file).Layout;
        Assert.That(layout.Duration, Is.EqualTo(new DurationOutcome.Known(samples, 44100, basis)), file);
        Assert.That(layout.Conditions.OfType<DurationUnusable>(), Is.Empty);
    }

    [TestCase("seeds/flac.flac")]
    [TestCase("seeds/flac-minimal.flac")]
    [TestCase("flac/comments.flac")]
    [TestCase("flac/id3v1-suffix.flac")]
    [TestCase("flac/ape-suffix.flac")]
    [TestCase("flac/id3v2-prefix.flac")]
    public void StreamInfosTotal_ConfirmedByTheLastFrame(string file) =>
        AssertKnown(file, Whole, DurationBasis.StreamInfoConfirmed);

    // The stream ends in a block of 2,170 samples, shorter than the rest, which only the last block may be.
    [TestCase("seeds/flac-piped.flac")]
    [TestCase("flac/streaminfo-total-zero.flac")]
    [TestCase("flac/streaminfo-total-doubled.flac")]
    public void AShortFinalBlock_EndsTheStream_WhateverStreamInfoSays(string file) =>
        AssertKnown(file, Whole, DurationBasis.ShortFinalBlock);

    [Test]
    public void AFrameCutOffByTheEndOfTheFile_IsATruncation()
    {
        // Frame 19's header is intact and its body runs past the end: the timeline ends where it began.
        AssertKnown("flac/truncated.flac", 19 * 4096, DurationBasis.Truncated);
        Assert.That(((DurationOutcome.Known)AudioCorpus.Analyse("flac/truncated.flac").Layout.Duration).WholeSeconds, Is.EqualTo(1));
    }

    [Test]
    public void JunkAfterTheLastFrame_IsReported_AndDoesNotCount()
    {
        AssertKnown("flac/garbage-appended.flac", Whole, DurationBasis.StreamInfoConfirmed);
        Assert.That(AudioCorpus.Analyse("flac/garbage-appended.flac").Layout.Conditions,
            Is.EquivalentTo(new Condition[] { new BytesAfterAudio(Region.Between(46_603, 49_603)) }));
    }

    [Test]
    public void DamageInTheMiddle_DoesNotShortenTheTimeline() =>
        AssertKnown("flac/zeroed-4k.flac", Whole, DurationBasis.StreamInfoConfirmed);

    [Test]
    public void BytesHoldingNoFrameAfterTheLastThatCanBeRead_LeaveThePlayingTimeUnusable()
    {
        var layout = AudioCorpus.Analyse("flac/last-frame-damaged.flac").Layout;

        Assert.That(layout.Duration, Is.EqualTo(new DurationOutcome.Unusable(DurationProblem.LastFramesUnaccounted)));
        Assert.That(layout.Conditions.OfType<DurationUnusable>().Single().Problem, Is.EqualTo(DurationProblem.LastFramesUnaccounted));
        Assert.That(layout.IsIncomplete, Is.True);
    }

    [Test]
    public void AFullFinalBlockEndingWhereTheAudioDoes_IsTheEnd_WithNoTotal()
    {
        var stream = Stream(0, [4096, 4096, 4096]);
        Assert.That(AudioCorpus.AnalyseFlac(stream).Layout.Duration, Is.EqualTo(new DurationOutcome.Known(3 * 4096, 44100, DurationBasis.EndsAtTrailingTags)));
    }

    [Test]
    public void AFullFinalBlockFollowedByJunk_IsUnusable_WithNoTotalToConfirmIt()
    {
        byte[] stream = [.. Stream(0, [4096, 4096, 4096]), .. Enumerable.Repeat((byte)0x5A, 500)];
        Assert.That(AudioCorpus.AnalyseFlac(stream).Layout.Duration, Is.EqualTo(new DurationOutcome.Unusable(DurationProblem.LastFramesUnaccounted)));
    }

    [Test]
    public void AStreamOfVariableBlockSize_EndsAtItsLastSampleNumber()
    {
        int[] sizes = [1000, 3000, 2000, 4608, 17];
        var stream = Stream((ulong)sizes.Sum(), sizes, variable: true);
        Assert.That(AudioCorpus.AnalyseFlac(stream).Layout.Duration, Is.EqualTo(new DurationOutcome.Known(sizes.Sum(), 44100, DurationBasis.StreamInfoConfirmed)));
    }

    [Test]
    public void ASampleRateRecordedNowhere_IsUnusable()
    {
        var stream = Stream(8192, [4096, 4096], streamRate: 0, frameRateCode: 0);
        Assert.That(AudioCorpus.AnalyseFlac(stream).Layout.Duration, Is.EqualTo(new DurationOutcome.Unusable(DurationProblem.NoSampleRate)));
    }

    [Test]
    public void ASampleRateRecordedOnlyInStreamInfo_IsUsed()
    {
        var stream = Stream(8192, [4096, 4096], streamRate: 22050, frameRateCode: 0);
        Assert.That(AudioCorpus.AnalyseFlac(stream).Layout.Duration, Is.EqualTo(new DurationOutcome.Known(8192, 22050, DurationBasis.StreamInfoConfirmed)));
    }
}
