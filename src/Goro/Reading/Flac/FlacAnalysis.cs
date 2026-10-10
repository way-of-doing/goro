using Goro.Reading.Bytes;
using Goro.Reading.Tags;

namespace Goro.Reading.Flac;

/// <summary>
/// The analysis of a FLAC file's edges: the tags around it, the <c>fLaC</c> marker, the metadata
/// block chain and its comments, where the audio starts, and its playing time, read through a
/// <see cref="BoundedReader"/> so that the middle of the file is never walked. The positions are
/// those of docs/design/flac-layout.md.
/// </summary>
/// <remarks>
/// The head and the tail are read first, as for an MP3, by the same <see cref="TagEdges"/>: no
/// specification allows Id3v2, APE or Id3v1 around a FLAC stream, and files carry them anyway. None
/// of it throws over what a file contains, only when the file cannot be read at all.
/// </remarks>
public static class FlacAnalysis
{
    private static readonly byte[] Marker = [.. "fLaC"u8];

    /// <summary>How much a search reads at a time; the marker and the first frame nearly always lie where they are expected.</summary>
    private const int SearchStep = 16 * 1024;

    /// <summary>How much of a frame is read to confirm it by its body, where STREAMINFO gives no larger frame size.</summary>
    private const int FrameReach = 64 * 1024;

    public static FileLayout Analyse(BoundedReader reader)
    {
        var tags = new List<TagIndex>();
        var conditions = new List<Condition>();
        var head = TagEdges.ReadLeading(reader, tags, conditions);
        var trailingTagsStart = TagEdges.ReadTrailing(reader, head.End, tags, conditions);

        var gaveUp = false;
        long? marker = StartsWithMarker(reader, head.End) ? head.End : FindMarker(reader, head.SearchFrom, trailingTagsStart, out gaveUp);
        if (marker is not { } c)
        {
            return Layout(reader, tags, conditions, new AudioLocation.NotFound(gaveUp ? AudioNotFoundReason.SearchGaveUp : AudioNotFoundReason.NoFrame),
                new DurationOutcome.Unusable(DurationProblem.NoAudio), new FormatDetails.Flac(null, null));
        }

        if (head.Trusted && c > head.End)
        {
            conditions.Add(new JunkBeforeAudio(Region.Between(head.End, c)));
        }

        var metadata = FlacMetadata.Read(reader, c + 4);
        conditions.AddRange(metadata.Conditions);
        foreach (var block in metadata.Blocks.Where(block => block.Comments is not null))
        {
            var read = VorbisCommentIndex.Index(block.Comments!, block.Payload);
            tags.Add(read.Tag);
            conditions.AddRange(read.Conditions);
        }

        var firstFrame = metadata.End ?? FindFirstFrame(reader, metadata.LostAt, trailingTagsStart, metadata.StreamInfo, out gaveUp);
        var details = new FormatDetails.Flac(c, metadata);
        if (firstFrame is not { } d || d >= trailingTagsStart)
        {
            return Layout(reader, tags, conditions, new AudioLocation.NotFound(gaveUp ? AudioNotFoundReason.SearchGaveUp : AudioNotFoundReason.NoFrame),
                new DurationOutcome.Unusable(DurationProblem.NoAudio), details);
        }

        var duration = FlacDuration.Decide(reader, metadata.StreamInfo, d, trailingTagsStart, conditions);
        if (duration is DurationOutcome.Unusable(var problem))
        {
            conditions.Add(new DurationUnusable(problem));
        }

        return Layout(reader, tags, conditions, new AudioLocation.Found(d, trailingTagsStart), duration, details);
    }

    private static FileLayout Layout(
        BoundedReader reader, List<TagIndex> tags, List<Condition> conditions, AudioLocation audio, DurationOutcome duration, FormatDetails details)
    {
        var ordered = TagEdges.MarkSources(tags, conditions);

        // Whatever a budget kept the analysis from reading, the file is reported for it.
        foreach (var purpose in (ReadPurpose[])[ReadPurpose.Sniff, ReadPurpose.DeclaredStructure, ReadPurpose.Search])
        {
            if (reader.Log.WentOverLimit(purpose))
            {
                conditions.Add(new ReadLimitReached(purpose));
            }
        }

        return new FileLayout(AudioFormat.Flac, ordered, audio, duration, details, [.. conditions], reader.Log);
    }

    private static bool StartsWithMarker(BoundedReader reader, long at) =>
        reader.TryRead(ReadPurpose.Sniff, at, Marker.Length, out var bytes) && bytes.Span.SequenceEqual(Marker);

    /// <summary>
    /// The <c>fLaC</c> marker in [<paramref name="from"/>, <paramref name="to"/>), behind a tag whose
    /// size cannot be trusted or junk after one: the four letters, then a STREAMINFO block header,
    /// which is <c>00 00 00 22</c>, or <c>80 00 00 22</c> where it is the only block.
    /// </summary>
    private static long? FindMarker(BoundedReader reader, long from, long to, out bool gaveUp)
    {
        gaveUp = false;
        const int pattern = 8;
        for (var at = from; at < to; at += SearchStep)
        {
            var length = Math.Min(SearchStep + pattern - 1, to - at);
            if (!reader.TryRead(ReadPurpose.Search, at, length, out var block))
            {
                gaveUp = true;
                return null;
            }

            var span = block.Span;
            for (var i = span.IndexOf(Marker); i >= 0 && i + pattern <= span.Length; )
            {
                if ((span[i + 4] & 0x7F) == 0 && span[i + 5] == 0 && span[i + 6] == 0 && span[i + 7] == 0x22)
                {
                    return at + i;
                }

                var next = span[(i + 1)..].IndexOf(Marker);
                i = next < 0 ? -1 : i + 1 + next;
            }
        }

        return null;
    }

    /// <summary>
    /// The first frame in [<paramref name="from"/>, <paramref name="to"/>), where the chain was lost:
    /// a header numbered 0 whose body ends where its CRC-16 says, confirmed that way rather than by
    /// the frame after it, since a stream can be a single frame.
    /// </summary>
    private static long? FindFirstFrame(BoundedReader reader, long from, long to, StreamInfo? streamInfo, out bool gaveUp)
    {
        gaveUp = false;
        var reach = Math.Max(FrameReach, (streamInfo?.MaxFrameSize ?? 0) + FlacFrameHeader.MaxLength);
        for (var at = from; at < to; at += SearchStep)
        {
            if (!reader.TryRead(ReadPurpose.Search, at, Math.Min(SearchStep, to - at), out var block))
            {
                gaveUp = true;
                return null;
            }

            var span = block.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == 0xFF && IsFirstFrame(reader, at + i, Math.Min(to, at + i + reach), streamInfo))
                {
                    return at + i;
                }
            }
        }

        return null;
    }

    private static bool IsFirstFrame(BoundedReader reader, long at, long limit, StreamInfo? streamInfo) =>
        FlacMetadata.IsFirstFrame(reader, at)
        && reader.TryRead(ReadPurpose.Search, at, limit - at, out var frame)
        && FlacFrameHeader.TryParse(frame.Span, out var header)
        && FlacFrameBody.FindEnd(frame.Span, header, streamInfo?.BitDepth ?? 0) is FlacFrameEnd.EndsAt { CrcPasses: true };
}
