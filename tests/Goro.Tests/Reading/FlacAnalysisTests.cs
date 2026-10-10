using Goro.Reading;
using Goro.Reading.Flac;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// The analysis of a FLAC file's edges (docs/design/flac-layout.md): where the marker and the audio
/// are, which tags are each format's source, and which damage counts the file as incomplete. The
/// positions expected are the ones flac-layout measured with a probe of its own: <c>C</c>, the
/// marker; <c>D</c>, the first frame; <c>F</c>, where the trailing tags start.
/// </summary>
public class FlacAnalysisTests
{
    [TestCase("seeds/flac.flac", 0, 8304, 50411)]
    [TestCase("seeds/flac-minimal.flac", 0, 86, 42193)]
    [TestCase("seeds/flac-piped.flac", 0, 8282, 50389)]
    [TestCase("flac/comments.flac", 0, 4496, 46603)]
    [TestCase("flac/comment-block-last.flac", 0, 1259, 43366)]
    [TestCase("flac/two-comment-blocks.flac", 0, 286, 42393)]
    [TestCase("flac/id3v2-prefix.flac", 114, 4610, 46717)]
    [TestCase("flac/id3v1-suffix.flac", 0, 4496, 46603)]
    [TestCase("flac/ape-suffix.flac", 0, 4496, 46603)]
    [TestCase("flac/garbage-appended.flac", 0, 4496, 49603)]
    [TestCase("flac/truncated.flac", 0, 4496, 27961)]
    [TestCase("flac/dmg-block-length-overrun.flac", 0, 1259, 43366)]
    [TestCase("flac/dmg-no-last-block-flag.flac", 0, 491, 42598)]
    [TestCase("flac/rw-comment-block-too-short.flac", 0, 193, 42300)]
    [TestCase("flac/rw-picture-block-length-zero.flac", 0, 1319, 43426)]
    [TestCase("flac/rw-streaminfo-short.flac", 0, 1184, 43291)]
    [TestCase("flac/dmg-application-length-overrun.flac", 0, 1402, 43509)]
    [TestCase("flac/dmg-id3v2-size-short.flac", 604, 5100, 47207)]
    [TestCase("flac/id3v2-junk-before-marker.flac", 1114, 5610, 47717)]
    public void TheMarkerAndTheAudio_AreWhereFlacLayoutMeasuredThem(string file, long marker, long firstFrame, long trailingTagsStart)
    {
        var layout = AudioCorpus.Analyse(file).Layout;

        Assert.That(layout.Format, Is.EqualTo(AudioFormat.Flac));
        Assert.That(((FormatDetails.Flac)layout.Details).Marker, Is.EqualTo(marker));
        Assert.That(layout.Audio, Is.EqualTo(new AudioLocation.Found(firstFrame, trailingTagsStart)));
    }

    [TestCase("flac/comments.flac")]
    [TestCase("flac/comment-block-last.flac")]
    [TestCase("flac/id3v2-prefix.flac")]
    [TestCase("flac/ape-suffix.flac")]
    [TestCase("flac/rw-picture-block-and-comment.flac")]
    public void AHealthyFile_IsNotIncomplete_AndReportsNothing(string file)
    {
        var layout = AudioCorpus.Analyse(file).Layout;
        Assert.That(layout.Conditions, Is.Empty);
        Assert.That(layout.IsIncomplete, Is.False);
    }

    [Test]
    public void TagsAroundTheStream_AreEachTheirFormatsSource()
    {
        var prefixed = AudioCorpus.Analyse("flac/id3v2-prefix.flac");
        Assert.That(prefixed.Source(TagFormat.Id3v2).Fields, Is.Not.Empty);
        Assert.That(prefixed.Text(prefixed.Field(TagFormat.Vorbis, "TITLE")), Is.EqualTo(new[] { "Title ✓" }));

        var suffixed = AudioCorpus.Analyse("flac/ape-suffix.flac");
        Assert.That(suffixed.Layout.Tags.Select(t => t.Kind.Format), Is.EqualTo(new[] { TagFormat.Vorbis, TagFormat.Ape, TagFormat.Id3v1 }));
    }

    [Test]
    public void OfTwoCommentBlocks_TheFirstIsTheSource_AndTheSecondIsReported()
    {
        var analysed = AudioCorpus.Analyse("flac/two-comment-blocks.flac");

        Assert.That(analysed.Text(analysed.Field(TagFormat.Vorbis, "TITLE")), Is.EqualTo(new[] { "Title ✓" }));
        Assert.That(analysed.Fields(TagFormat.Vorbis, "ARTIST").Select(analysed.Text), Is.EqualTo(new[] { new[] { "AC/DC" }, new[] { "Motörhead" } }));
        Assert.That(analysed.Layout.Conditions.OfType<FurtherTag>().Single().Tag.Format, Is.EqualTo(TagFormat.Vorbis));
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    [Test]
    public void JunkBeforeTheMarker_IsReported_AndTheStreamFound()
    {
        var layout = AudioCorpus.Analyse("flac/id3v2-junk-before-marker.flac").Layout;
        Assert.That(layout.Conditions, Is.EquivalentTo(new Condition[] { new JunkBeforeAudio(Region.Between(114, 1114)) }));
    }

    [TestCase("flac/dmg-no-last-block-flag.flac")]
    [TestCase("flac/dmg-block-length-overrun.flac")]
    [TestCase("flac/rw-comment-block-too-short.flac")]
    [TestCase("flac/rw-picture-block-length-zero.flac")]
    [TestCase("flac/rw-streaminfo-short.flac")]
    [TestCase("flac/dmg-comment-values.flac")]
    public void AFaultThatLosesNothing_DoesNotMakeTheFileIncomplete(string file)
    {
        var layout = AudioCorpus.Analyse(file).Layout;
        Assert.That(layout.IsIncomplete, Is.False, string.Join(", ", layout.Conditions));
        Assert.That(layout.Conditions, Is.Not.Empty, "it is still reported, for goro audit");
    }

    [TestCase("flac/dmg-application-length-overrun.flac", typeof(MetadataChainLost))]
    [TestCase("flac/dmg-comment-length-overrun.flac", typeof(TagStructureBroken))]
    [TestCase("flac/dmg-zeroed-comment.flac", typeof(TagStructureBroken))]
    [TestCase("flac/last-frame-damaged.flac", typeof(DurationUnusable))]
    public void DamageThatLosesSomething_MakesTheFileIncomplete(string file, Type condition)
    {
        var layout = AudioCorpus.Analyse(file).Layout;
        Assert.That(layout.IsIncomplete, Is.True);
        Assert.That(layout.Conditions.Where(c => c.MakesIncomplete).Select(c => c.GetType()), Is.EquivalentTo(new[] { condition }));
    }

    [Test]
    public void ALostChain_StillGivesTheCommentsBeforeTheBreak()
    {
        var analysed = AudioCorpus.Analyse("flac/dmg-application-length-overrun.flac");
        Assert.That(analysed.Source(TagFormat.Vorbis).Fields.Length, Is.EqualTo(10));
    }

    [Test]
    public void ADamagedIdv2SizeBeforeTheMarker_LeavesTheStreamToBeFound()
    {
        var analysed = AudioCorpus.Analyse("flac/dmg-id3v2-size-short.flac");
        Assert.That(analysed.Source(TagFormat.Vorbis).Fields.Length, Is.EqualTo(10));
        Assert.That(analysed.Layout.Duration, Is.InstanceOf<DurationOutcome.Known>());
    }

    [Test]
    public void AnMp3ReadAsFlac_HasNoAudio()
    {
        var layout = AudioCorpus.AnalyseFlac(AudioCorpus.Bytes("seeds/mp3-cbr.mp3")).Layout;
        Assert.That(layout.Audio, Is.EqualTo(new AudioLocation.NotFound(AudioNotFoundReason.NoFrame)));
        Assert.That(layout.Duration, Is.EqualTo(new DurationOutcome.Unusable(DurationProblem.NoAudio)));
    }

    [Test]
    public void AFileEndingInItsMetadata_HasNoAudio()
    {
        var layout = AudioCorpus.AnalyseFlac(AudioCorpus.Bytes("flac/comments.flac")[..4000]).Layout;
        Assert.That(layout.Audio, Is.InstanceOf<AudioLocation.NotFound>());
    }
}
