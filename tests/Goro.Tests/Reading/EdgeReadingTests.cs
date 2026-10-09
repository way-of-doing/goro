using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// Reads stay at the edges (docs/testing.md, "Reading audio files"): on a long file, the analysis and
/// every value read take a bounded amount from the source, however long the audio between them.
/// The log says what was read, so this is asserted rather than inferred from timing.
/// </summary>
public class EdgeReadingTests
{
    private const long Audio = 200L * 1024 * 1024;

    [Test]
    public void ALongFile_IsReadOnlyAtItsEdges_TagValuesIncluded()
    {
        // The tags of a corpus file that has them at both ends, around 200 MB of frames.
        var corpus = AudioCorpus.Bytes("mp3/all-tags.mp3");
        var found = (AudioLocation.Found)AudioCorpus.Analyse(corpus).Layout.Audio;
        var source = new TiledSource(corpus[..(int)found.FirstFrame], SyntheticMp3Builder.BuildAudioFrames(1), Audio, corpus[(int)found.TrailingTagsStart..]);
        var reader = new BoundedReader(source, ReadPolicy.Default);

        var layout = Mp3Analysis.Analyse(reader);
        var values = new TagValues(reader);
        foreach (var field in layout.Tags.SelectMany(tag => tag.Fields))
        {
            values.Bytes(field);
            values.Text(field);
            values.Description(field);
        }

        Assert.That(layout.Audio, Is.InstanceOf<AudioLocation.Found>());
        Assert.That(layout.Tags.Select(t => t.Kind.Format), Is.EquivalentTo(new[] { TagFormat.Id3v2, TagFormat.Ape, TagFormat.Id3v1 }));
        // The first frame follows the leading tag, inside the head window. The frames carry no summary
        // header, so the stream is counted from its edges: besides the two windows, seven probes of the
        // middle and the headers that confirm what they find, and nothing else, cover art included.
        Assert.That(layout.Duration, Is.InstanceOf<DurationOutcome.Known>()
            .With.Property(nameof(DurationOutcome.Known.Basis)).EqualTo(DurationBasis.ConstantBitrateFromEdges));
        var windows = ReadPolicy.Default.HeadWindow + ReadPolicy.Default.TailWindow;
        Assert.That(reader.Log.BytesFromSource, Is.InRange(windows, windows + 7 * (16 * 1024 + 64)));
        Assert.That(reader.Log.Records.Where(r => r.Outcome == ReadOutcome.FromSource && r.Purpose != ReadPurpose.Sniff),
            Has.All.Property(nameof(ReadRecord.Purpose)).EqualTo(ReadPurpose.Search), "only the windows and the probes");
    }

    [Test]
    public void AFirstFrameFarBehindItsTag_IsSearchedForInSteps_NoFurtherThanTheSearchBudget()
    {
        var tag = SyntheticMp3Builder.BuildId3V2(100);
        byte[] gap = new byte[300 * 1024];
        var source = new TiledSource([.. tag, .. gap], SyntheticMp3Builder.BuildAudioFrames(1), Audio, []);
        var reader = new BoundedReader(source, ReadPolicy.Default);

        var layout = Mp3Analysis.Analyse(reader);

        Assert.That(layout.Audio, Is.EqualTo(new AudioLocation.NotFound(AudioNotFoundReason.SearchGaveUp)));
        var searched = reader.Log.Records.Where(r => r.Purpose == ReadPurpose.Search && r.Outcome == ReadOutcome.FromSource).Sum(r => r.Length);
        Assert.That(searched, Is.LessThanOrEqualTo(ReadPolicy.Default.SearchBudget));
    }

    // A stream whose bitrate changes is not counted, and not walked either: the first probe that finds
    // another bitrate ends the attempt.
    [Test]
    public void ALongFileWithoutAHeader_ThatIsNotConstantBitrate_HasNoDuration_AndIsNotWalked()
    {
        byte[] tile = [.. SyntheticMp3Builder.BuildAudioFrames(3), .. OtherBitrateFrame()];
        var source = new TiledSource(SyntheticMp3Builder.BuildId3V2(100), tile, Audio, []);
        var reader = new BoundedReader(source, ReadPolicy.Default);

        var layout = Mp3Analysis.Analyse(reader);

        Assert.That(layout.Duration, Is.EqualTo(new DurationOutcome.Unusable(DurationProblem.NotConstantBitrate)));
        Assert.That(layout.IsIncomplete, Is.True);
        Assert.That(reader.Log.BytesFromSource, Is.LessThan(ReadPolicy.Default.AnalysisCeiling));
    }

    /// <summary>One MPEG-1 Layer III frame at 160 kbps, 44.1 kHz, mono: 522 bytes.</summary>
    private static byte[] OtherBitrateFrame()
    {
        var frame = new byte[522];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = 0xA0;
        frame[3] = 0xC0;
        return frame;
    }

    /// <summary>A file of <paramref name="head"/>, then <paramref name="tile"/> repeated for about <paramref name="middle"/> bytes, then <paramref name="tail"/>, made on demand.</summary>
    private sealed class TiledSource(byte[] head, byte[] tile, long middle, byte[] tail) : IByteSource
    {
        private readonly long middleLength = middle / tile.Length * tile.Length;

        public long Length => head.Length + middleLength + tail.Length;

        public int Read(long offset, Span<byte> into)
        {
            var count = (int)Math.Min(into.Length, Math.Max(0, Length - offset));
            for (var i = 0; i < count; i++)
            {
                var at = offset + i;
                into[i] = at < head.Length ? head[at]
                    : at < head.Length + middleLength ? tile[(at - head.Length) % tile.Length]
                    : tail[at - head.Length - middleLength];
            }

            return count;
        }
    }
}
