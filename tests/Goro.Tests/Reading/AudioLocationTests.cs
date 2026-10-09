using Goro.Reading;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>Where the analysis finds the audio, across the whole corpus.</summary>
public class AudioLocationTests
{
    /// <summary>The one MP3 in the corpus with no audio to find: it ends inside its Id3v2 tag.</summary>
    private const string NoAudio = "mp3/dmg-id3v24-truncated-in-tag.mp3";

    private static IEnumerable<TestCaseData> Mp3Files() =>
        AudioCorpus.Mp3Files.Where(file => file != NoAudio).Select(file => new TestCaseData(file).SetName($"audio found: {file}"));

    [TestCaseSource(nameof(Mp3Files))]
    public void EveryOtherMp3_HasItsAudioFound(string file)
    {
        var layout = AudioCorpus.Analyse(file).Layout;

        Assert.That(layout.Audio, Is.InstanceOf<AudioLocation.Found>());
        var found = (AudioLocation.Found)layout.Audio;
        Assert.That(found.FirstFrame, Is.LessThan(found.TrailingTagsStart));
        Assert.That(layout.Tags.Where(t => t.IsSource).All(t => t.Region.End <= found.FirstFrame || t.Region.Start >= found.TrailingTagsStart), Is.True,
            "no source tag overlaps the audio");
    }

    [TestCase("mp3/id3v24.mp3")]
    [TestCase("mp3/all-tags.mp3")]
    [TestCase("seeds/mp3-cbr-notag.mp3")]
    public void AHealthyFile_HasItsFirstFrameWhereItsLeadingTagsEnd_AndNoConditionsOfDamage(string file)
    {
        var layout = AudioCorpus.Analyse(file).Layout;

        // Each of these files has at most one leading tag.
        var leadingEnd = layout.Tags.Where(t => t.Region.Start == 0).Select(t => t.Region.End).SingleOrDefault();
        Assert.That(((AudioLocation.Found)layout.Audio).FirstFrame, Is.EqualTo(leadingEnd));
        Assert.That(layout.Conditions, Is.Empty);
    }

    [Test]
    public void AFreeFormatStream_IsFound()
    {
        var layout = AudioCorpus.Analyse("seeds/mp3-freeformat.mp3").Layout;

        Assert.That(layout.Audio, Is.InstanceOf<AudioLocation.Found>());
    }
}
