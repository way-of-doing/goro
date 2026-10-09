using System.Collections.Immutable;
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
    private const int Lyrics3FooterLength = 15;

    public static FileLayout Analyse(BoundedReader reader)
    {
        var tags = new List<TagIndex>();
        var conditions = new List<Condition>();
        var head = ReadHead(reader, tags, conditions);
        var trailingTagsStart = ReadTail(reader, head.End, tags, conditions);
        var audio = Collate(reader, head, trailingTagsStart, conditions);
        var ordered = MarkSources(tags, conditions);

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

        return new FileLayout(AudioFormat.Mp3, ordered, audio, duration, summary, [.. conditions], reader.Log);
    }

    /// <param name="End"><c>B</c>: where the leading tags say they end.</param>
    /// <param name="SearchFrom">Where to look for the first frame from: <c>B</c>, unless a tag's size could not be trusted.</param>
    private readonly record struct Head(long End, long SearchFrom, bool Trusted);

    /// <summary>Any number of Id3v2 tags, then an APE tag with a header, from the start of the file.</summary>
    private static Head ReadHead(BoundedReader reader, List<TagIndex> tags, List<Condition> conditions)
    {
        long pos = 0;
        while (true)
        {
            var read = Id3v2Index.TryRead(reader, pos) ?? ApeIndex.TryReadFromHeader(reader, pos);
            if (read is null)
            {
                return new Head(pos, pos, Trusted: true);
            }

            tags.Add(read.Tag);
            conditions.AddRange(read.Conditions);
            if (!read.SizeTrusted)
            {
                // Where the tag ends is unknown, so the audio is searched for from just after its
                // header, and nothing further is looked for at the head.
                var afterHeader = read.Tag.Region.End;
                return new Head(afterHeader, afterHeader, Trusted: false);
            }

            pos = read.Tag.Region.End;
        }
    }

    /// <summary>
    /// The trailing tags, from the end inwards: Id3v1, Lyrics3v2, APE by its footer, and an Id3v2 tag
    /// appended with a footer. Returns <c>F</c>, where they start.
    /// </summary>
    private static long ReadTail(BoundedReader reader, long floor, List<TagIndex> tags, List<Condition> conditions)
    {
        var end = reader.Length;
        var id3v1Seen = false;
        while (true)
        {
            if (!id3v1Seen && end - Id3v1Index.Length >= floor && Id3v1Index.TryRead(reader, end - Id3v1Index.Length) is { } id3v1)
            {
                id3v1Seen = true;
                tags.Add(id3v1.Tag);
                end = id3v1.Tag.Region.Start;
                continue;
            }

            if (TryLyrics3(reader, end, floor) is { } lyrics)
            {
                tags.Add(lyrics);
                end = lyrics.Region.Start;
                continue;
            }

            if (ApeIndex.TryReadFromFooter(reader, end, floor) is { } ape)
            {
                tags.Add(ape.Tag);
                conditions.AddRange(ape.Conditions);
                end = ape.Tag.Region.Start;
                continue;
            }

            if (TryAppendedId3v2(reader, end, floor) is { } appended)
            {
                tags.Add(appended.Tag);
                conditions.AddRange(appended.Conditions);
                end = appended.Tag.Region.Start;
                continue;
            }

            return end;
        }
    }

    /// <summary>A Lyrics3v2 block ending at <paramref name="end"/>: <c>LYRICSBEGIN</c>, fields, a six-digit size and <c>LYRICS200</c>.</summary>
    private static TagIndex? TryLyrics3(BoundedReader reader, long end, long floor)
    {
        var footerAt = end - Lyrics3FooterLength;
        if (footerAt < floor
            || !reader.TryRead(ReadPurpose.Sniff, footerAt, Lyrics3FooterLength, out var footer)
            || !footer.Span[6..].SequenceEqual("LYRICS200"u8)
            || !int.TryParse(footer.Span[..6], System.Globalization.NumberStyles.None, null, out var size))
        {
            return null;
        }

        var start = footerAt - size;
        return start >= floor
            && reader.TryRead(ReadPurpose.Sniff, start, 11, out var begin)
            && begin.Span.SequenceEqual("LYRICSBEGIN"u8)
                ? new TagIndex(new TagKind(TagFormat.Lyrics3, 2), Region.Between(start, end), TagState.Intact, [])
                : null;
    }

    /// <summary>An Id3v2 tag whose footer (<c>3DI</c>) ends at <paramref name="end"/>, which v2.4 allows after the audio.</summary>
    private static TagRead? TryAppendedId3v2(BoundedReader reader, long end, long floor)
    {
        const int footerLength = 10;
        if (end - footerLength < floor
            || !reader.TryRead(ReadPurpose.Sniff, end - footerLength, footerLength, out var footer)
            || !footer.Span.StartsWith("3DI"u8)
            || !Binary.IsSyncsafe(footer.Span[6..]))
        {
            return null;
        }

        var at = end - footerLength - Binary.Syncsafe(footer.Span[6..]) - 10;
        return at >= floor && Id3v2Index.TryRead(reader, at) is { SizeTrusted: true } read && read.Tag.Region.End == end ? read : null;
    }

    /// <summary>Searches between the tags for the first frame, <c>C</c>.</summary>
    private static AudioLocation Collate(BoundedReader reader, Head head, long trailingTagsStart, List<Condition> conditions)
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

    /// <summary>
    /// Puts the tags in file order and makes the first of each format its source, as players and
    /// other readers take it; any later one is reported.
    /// </summary>
    private static ImmutableArray<TagIndex> MarkSources(List<TagIndex> tags, List<Condition> conditions)
    {
        var ordered = tags.OrderBy(tag => tag.Region.Start).ToList();
        var seen = new HashSet<TagFormat>();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!seen.Add(ordered[i].Kind.Format))
            {
                ordered[i] = ordered[i] with { IsSource = false };
                conditions.Add(new FurtherTag(ordered[i].Kind, ordered[i].Region));
            }
        }

        return [.. ordered];
    }
}
