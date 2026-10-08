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
        // The first frame follows the leading tag, inside the head window, so the two windows are all
        // that is read, cover art included.
        Assert.That(reader.Log.BytesFromSource, Is.EqualTo(ReadPolicy.Default.HeadWindow + ReadPolicy.Default.TailWindow));
        Assert.That(reader.Log.Records.Count(r => r.Outcome == ReadOutcome.FromSource), Is.EqualTo(2));
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
