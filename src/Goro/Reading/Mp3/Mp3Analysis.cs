using Goro.Reading.Bytes;
using Goro.Reading.Tags;

namespace Goro.Reading.Mp3;

/// <summary>
/// The analysis of an MP3 file's edges: the tags at either end, and where the audio starts, read
/// through a <see cref="BoundedReader"/> so that the middle of the file is never walked. The
/// positions are those of docs/design/mp3-layout.md.
/// </summary>
/// <remarks>
/// Three steps run one after another: the head, read from the start of the file onwards; the tail,
/// read from the end inwards; and a collation that searches for the first frame between them. None
/// throws over what a file contains, only when the file cannot be read at all.
/// </remarks>
public static class Mp3Analysis
{
    public static FileLayout Analyse(BoundedReader reader)
    {
        var tags = new List<TagIndex>();
        var conditions = new List<Condition>();
        var head = TagEdges.ReadLeading(reader, tags, conditions);
        var trailingTagsStart = TagEdges.ReadTrailing(reader, head.End, tags, conditions);
        var audio = Collate(reader, head, trailingTagsStart, conditions);
        var ordered = TagEdges.MarkSources(tags, conditions);

        var (duration, summary) = audio is AudioLocation.Found found
            ? Mp3Duration.Decide(reader, found.FirstFrame, found.TrailingTagsStart, conditions)
            : (new DurationOutcome.Unusable(DurationProblem.NoAudio), null);
        if (audio is AudioLocation.Found && duration is DurationOutcome.Unusable(var problem))
        {
            conditions.Add(new DurationUnusable(problem));
        }

        // Whatever a budget kept the analysis from reading, the file is reported for it.
        foreach (var purpose in (ReadPurpose[])[ReadPurpose.Sniff, ReadPurpose.DeclaredStructure, ReadPurpose.Search])
        {
            if (reader.Log.WentOverLimit(purpose))
            {
                conditions.Add(new ReadLimitReached(purpose));
            }
        }

        return new FileLayout(AudioFormat.Mp3, ordered, audio, duration, new FormatDetails.Mp3(summary), [.. conditions], reader.Log);
    }

    /// <summary>Searches between the tags for the first frame, <c>C</c>.</summary>
    private static AudioLocation Collate(BoundedReader reader, LeadingTags head, long trailingTagsStart, List<Condition> conditions)
    {
        if (MpegFrames.FindFirst(reader, head.SearchFrom, trailingTagsStart, out var gaveUp) is not { } first)
        {
            return new AudioLocation.NotFound(gaveUp ? AudioNotFoundReason.SearchGaveUp : AudioNotFoundReason.NoFrame);
        }

        if (head.Trusted && first > head.End)
        {
            conditions.Add(new JunkBeforeAudio(Region.Between(head.End, first)));
        }

        return new AudioLocation.Found(first, trailingTagsStart);
    }
}
