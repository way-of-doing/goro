using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>Where the hash range starts and ends: <c>D</c> and <c>E</c> of docs/design/mp3-layout.md.</summary>
public class Mp3AudioRangeTests
{
    private static (FileLayout Layout, Region Range) Find(byte[] file)
    {
        var source = new MemoryByteSource(file);
        var layout = Mp3Analysis.Analyse(new BoundedReader(source, ReadPolicy.Default));
        return (layout, Mp3AudioRange.Find(source, layout));
    }

    private static AudioLocation.Found Audio(FileLayout layout) => (AudioLocation.Found)layout.Audio;

    [Test]
    public void AHealthyFile_RunsFromTheFrameAfterItsInfoFrame_ToWhereItsTrailingTagsStart()
    {
        var file = AudioCorpus.Bytes("mp3/id3v24.mp3");
        var (layout, range) = Find(file);
        var audio = Audio(layout);
        MpegFrameHeader.TryParse(file.AsSpan((int)audio.FirstFrame), out var infoFrame);

        Assert.That(layout.Summary?.Kind, Is.EqualTo(SummaryKind.Info));
        Assert.That(range.Start, Is.EqualTo(audio.FirstFrame + infoFrame.FrameLength), "D is the frame after the Info frame");
        Assert.That(range.End, Is.EqualTo(audio.TrailingTagsStart));
    }

    [Test]
    public void AFileWithoutASummaryHeader_StartsAtItsFirstFrame()
    {
        var (layout, range) = Find(AudioCorpus.Bytes("seeds/mp3-cbr-notag.mp3"));

        Assert.That(range.Start, Is.EqualTo(Audio(layout).FirstFrame));
        Assert.That(range.End, Is.EqualTo(Audio(layout).TrailingTagsStart));
    }

    [Test]
    public void AFrameCutShort_IsLeftOut()
    {
        var (layout, range) = Find(AudioCorpus.Bytes("mp3/cbr-truncated-mid-frame.mp3"));

        Assert.That(range.End, Is.LessThan(Audio(layout).TrailingTagsStart));
        Assert.That(Audio(layout).TrailingTagsStart - range.End, Is.LessThan(MpegFrameHeader.MaxFrameLength));
    }

    [Test]
    public void JunkAppended_IsLeftOut_WithTheSummaryHeader_AndWithout()
    {
        var file = AudioCorpus.Bytes("mp3/garbage-appended.mp3");
        var (layout, range) = Find(file);
        var junk = layout.Conditions.OfType<BytesAfterAudio>().Single().Region;

        Assert.That(range.End, Is.EqualTo(junk.Start));

        // The same file with its Info frame unrecognisable: the walk alone must stop before the junk.
        var withoutHeader = (byte[])file.Clone();
        var info = withoutHeader.AsSpan().IndexOf("Info"u8);
        withoutHeader[info] = (byte)'X';
        withoutHeader[info + 1] = (byte)'X';
        var (_, walked) = Find(withoutHeader);

        Assert.That(walked.End, Is.EqualTo(junk.Start));
    }

    // Two frames that confirm each other by chance, in junk after the audio, are not audio; three in
    // a row would be taken for it, which is why it takes three.
    [TestCase(2, false)]
    [TestCase(3, true)]
    public void ChanceFramesInJunkAfterTheAudio_CountOnlyAsARunOfThree(int chanceFrames, bool counted)
    {
        var audio = SyntheticMp3Builder.BuildAudioFrames(20);
        byte[] junk = [.. new byte[100], .. SyntheticMp3Builder.BuildAudioFrames(chanceFrames), .. new byte[100]];
        byte[] file = [.. audio, .. junk];

        var (_, range) = Find(file);

        Assert.That(range.End, Is.EqualTo(counted ? audio.Length + 100 + chanceFrames * 417 : audio.Length));
    }

    [Test]
    public void DamageInTheMiddle_StaysInsideTheRange()
    {
        var audio = SyntheticMp3Builder.BuildAudioFrames(20);
        audio.AsSpan(417 * 10, 417).Clear(); // a whole frame zeroed, header and all

        var (_, range) = Find(audio);

        Assert.That(range, Is.EqualTo(new Region(0, audio.Length)));
    }
}
