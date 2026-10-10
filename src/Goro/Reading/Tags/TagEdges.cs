using System.Collections.Immutable;
using Goro.Reading.Bytes;

namespace Goro.Reading.Tags;

/// <param name="End"><c>B</c>: where the leading tags say they end.</param>
/// <param name="SearchFrom">Where to look for the audio from: <c>B</c>, unless a tag's size could not be trusted.</param>
/// <param name="Trusted">Whether every leading tag's size could be trusted, so that bytes between <c>B</c> and the audio are junk.</param>
public readonly record struct LeadingTags(long End, long SearchFrom, bool Trusted);

/// <summary>
/// The tags at either edge of a file, which every format Goro reads may carry whatever its own
/// specification says: Id3v2 and APE in front, and behind the audio Id3v1, Lyrics3v2, APE and an
/// appended Id3v2 tag. The positions are those of docs/design/mp3-layout.md and flac-layout.md.
/// </summary>
public static class TagEdges
{
    private const int Lyrics3FooterLength = 15;

    /// <summary>Any number of Id3v2 tags, then an APE tag with a header, from the start of the file.</summary>
    public static LeadingTags ReadLeading(BoundedReader reader, List<TagIndex> tags, List<Condition> conditions)
    {
        long pos = 0;
        while (true)
        {
            var read = Id3v2Index.TryRead(reader, pos) ?? ApeIndex.TryReadFromHeader(reader, pos);
            if (read is null)
            {
                return new LeadingTags(pos, pos, Trusted: true);
            }

            tags.Add(read.Tag);
            conditions.AddRange(read.Conditions);
            if (!read.SizeTrusted)
            {
                // Where the tag ends is unknown, so the audio is searched for from just after its
                // header, and nothing further is looked for at the head.
                var afterHeader = read.Tag.Region.End;
                return new LeadingTags(afterHeader, afterHeader, Trusted: false);
            }

            pos = read.Tag.Region.End;
        }
    }

    /// <summary>
    /// The trailing tags, from the end inwards: Id3v1, Lyrics3v2, APE by its footer, and an Id3v2 tag
    /// appended with a footer. Returns <c>F</c>, where they start.
    /// </summary>
    public static long ReadTrailing(BoundedReader reader, long floor, List<TagIndex> tags, List<Condition> conditions)
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

    /// <summary>
    /// Puts the tags in file order and makes the first of each format its source, as players and
    /// other readers take it; any later one is reported.
    /// </summary>
    public static ImmutableArray<TagIndex> MarkSources(List<TagIndex> tags, List<Condition> conditions)
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
