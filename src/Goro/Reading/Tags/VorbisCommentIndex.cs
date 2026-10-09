using System.Collections.Immutable;
using System.Text;
using Goro.Reading.Bytes;

namespace Goro.Reading.Tags;

/// <summary>One comment as its list records it: where it starts, its name where it has one, and where its value lies.</summary>
/// <param name="At">Where the comment's length field starts.</param>
/// <param name="Region">The comment itself, name and value, after its length field, as long as its length says.</param>
/// <param name="Name">Its name, before the <c>=</c>; null where it has no <c>=</c>, or the name does not decode.</param>
/// <param name="HasSeparator">Whether it holds an <c>=</c> at all, as far as it could be read.</param>
/// <param name="Value">What follows the <c>=</c>; empty where there is none.</param>
public readonly record struct VorbisComment(long At, Region Region, string? Name, bool HasSeparator, Region Value);

/// <summary>Where reading a comment list stopped short of its end.</summary>
public enum VorbisListBreak
{
    /// <summary>The vendor string, or the count after it, runs past what may be read.</summary>
    VendorOverrun,

    /// <summary>A comment runs past what may be read: the last of <see cref="VorbisCommentList.Comments"/>.</summary>
    CommentOverrun,

    /// <summary>The budget for reading the structure ran out.</summary>
    ReadLimitReached,
}

/// <summary>
/// A Vorbis comment list as its own lengths describe it: a vendor string, a count, then each comment
/// with its length. <see cref="End"/> is where the list says it ends, which a FLAC block's recorded
/// length need not agree with; null where the list runs past what could be read, at
/// <see cref="BreakAt"/>.
/// </summary>
/// <param name="VendorEnd">Where the vendor string ends; null where its length could not be read.</param>
public sealed record VorbisCommentList(long Start, long? VendorEnd, ImmutableArray<VorbisComment> Comments, long? End, VorbisListBreak? Break, long BreakAt);

/// <summary>
/// Indexes a Vorbis comment list (RFC 9639, section 8.6, and the Vorbis I comment header) without
/// reading its values: lengths, and each comment's name up to its <c>=</c>. In a FLAC file the list
/// is a VORBIS_COMMENT block's payload, which has no framing bit.
/// </summary>
public static class VorbisCommentIndex
{
    /// <summary>How much of a comment is read at first to find its <c>=</c>; names are short.</summary>
    private const int NameProbe = 256;

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads the list starting at <paramref name="start"/>, no further than <paramref name="limit"/>.
    /// A comment running past the limit is still listed, with what of its name could be read.
    /// </summary>
    public static VorbisCommentList Read(BoundedReader reader, long start, long limit)
    {
        var comments = ImmutableArray.CreateBuilder<VorbisComment>();
        if (!TryU32(reader, start, limit, out var vendorLength, out var refused))
        {
            return new VorbisCommentList(start, null, [], null, refused ? VorbisListBreak.ReadLimitReached : VorbisListBreak.VendorOverrun, start);
        }

        var vendorEnd = start + 4 + vendorLength;
        if (!TryU32(reader, vendorEnd, limit, out var count, out refused))
        {
            return new VorbisCommentList(start, vendorEnd, [], null, refused ? VorbisListBreak.ReadLimitReached : VorbisListBreak.VendorOverrun, vendorEnd);
        }

        var at = vendorEnd + 4;
        for (var i = 0L; i < count; i++)
        {
            if (!TryU32(reader, at, limit, out var length, out refused))
            {
                return Broken(refused ? VorbisListBreak.ReadLimitReached : VorbisListBreak.CommentOverrun, at);
            }

            var region = new Region(at + 4, length);
            var readable = Region.Between(region.Start, Math.Min(region.End, limit));
            if (!TryName(reader, region, readable, out var comment))
            {
                return Broken(VorbisListBreak.ReadLimitReached, at);
            }

            comments.Add(comment with { At = at });
            if (region.End > limit)
            {
                return Broken(VorbisListBreak.CommentOverrun, at);
            }

            at = region.End;
        }

        return new VorbisCommentList(start, vendorEnd, comments.ToImmutable(), at, null, at);

        VorbisCommentList Broken(VorbisListBreak why, long where) => new(start, vendorEnd, comments.ToImmutable(), null, why, where);
    }

    /// <summary>
    /// The tag a list makes within <paramref name="block"/>, the region its FLAC block was decided to
    /// occupy, and what was found wrong with it (docs/features/builtins/identifiers.md, "Damaged data").
    /// </summary>
    public static TagRead Index(VorbisCommentList list, Region block)
    {
        var kind = new TagKind(TagFormat.Vorbis, 1);
        if (list.VendorEnd is not { } vendorEnd || vendorEnd > block.End)
        {
            return list.Break is VorbisListBreak.ReadLimitReached
                ? new TagRead(TagIndex.Unusable(kind, block), [new ReadLimitReached(ReadPurpose.DeclaredStructure)], SizeTrusted: true)
                : TagRead.Unusable(kind, block, TagUnusableReason.VendorRunsPastTag, sizeTrusted: true);
        }

        var conditions = new List<Condition>();
        var fields = ImmutableArray.CreateBuilder<FieldEntry>();
        TagStructureBroken? broken = null;
        foreach (var comment in list.Comments)
        {
            if (comment.Region.End > block.End)
            {
                // Clearly recorded, and unreadable: an unusable occurrence, where its name can be had.
                if (comment.Name is { } name)
                {
                    fields.Add(new FieldEntry(name, FieldForm.VorbisComment, new StoredBytes.InFile(comment.Value), RunsPastTag: true));
                }

                broken = new TagStructureBroken(kind, comment.At, StructureBreak.FieldRunsPastTag);
                break;
            }

            if (!comment.HasSeparator)
            {
                conditions.Add(new CommentWithoutName(comment.At));
                continue;
            }

            fields.Add(comment.Name is { } key
                ? new FieldEntry(key, FieldForm.VorbisComment, new StoredBytes.InFile(comment.Value))
                : new FieldEntry("", FieldForm.VorbisComment, new StoredBytes.InFile(comment.Value), NameUnreadable: true));
        }

        broken ??= list switch
        {
            { End: null, Break: VorbisListBreak.ReadLimitReached } => new TagStructureBroken(kind, list.BreakAt, StructureBreak.ReadLimitReached),
            { End: null } => new TagStructureBroken(kind, list.BreakAt, StructureBreak.FieldRunsPastTag),
            { End: { } end } when end < block.End => new TagStructureBroken(kind, end, StructureBreak.BytesAfterLastComment),
            _ => null,
        };
        if (broken is not null)
        {
            conditions.Add(broken);
        }

        var state = broken is null ? TagState.Intact : TagState.BrokenOff;
        return new TagRead(new TagIndex(kind, block, state, fields.ToImmutable()), [.. conditions], SizeTrusted: true);
    }

    /// <summary>
    /// Finds a comment's <c>=</c> within what of it can be read, <paramref name="readable"/>, and
    /// decodes its name, reading as little of the comment as that takes.
    /// </summary>
    private static bool TryName(BoundedReader reader, Region region, Region readable, out VorbisComment comment)
    {
        comment = new VorbisComment(0, region, null, false, new Region(region.End, 0));
        var probe = Math.Min(readable.Length, NameProbe);
        if (!reader.TryRead(ReadPurpose.DeclaredStructure, readable.Start, probe, out var head))
        {
            return false;
        }

        var separator = head.Span.IndexOf((byte)'=');
        if (separator < 0 && readable.Length > probe)
        {
            if (!reader.TryRead(ReadPurpose.DeclaredStructure, readable.Start, readable.Length, out var whole))
            {
                return false;
            }

            head = whole;
            separator = head.Span.IndexOf((byte)'=');
        }

        if (separator < 0)
        {
            return true;
        }

        string? name;
        try
        {
            name = StrictUtf8.GetString(head.Span[..separator]);
        }
        catch (DecoderFallbackException)
        {
            name = null;
        }

        comment = new VorbisComment(0, region, name, true, Region.Between(region.Start + separator + 1, region.End));
        return true;
    }

    /// <summary>A little-endian length, where it lies before <paramref name="limit"/> and the budget allows reading it.</summary>
    private static bool TryU32(BoundedReader reader, long at, long limit, out long value, out bool refused)
    {
        value = 0;
        refused = false;
        if (at + 4 > limit)
        {
            return false;
        }

        if (!reader.TryRead(ReadPurpose.DeclaredStructure, at, 4, out var bytes))
        {
            refused = true;
            return false;
        }

        value = Binary.U32LittleEndian(bytes.Span);
        return true;
    }
}
