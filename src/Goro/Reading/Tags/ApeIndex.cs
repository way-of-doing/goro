using System.Collections.Immutable;
using System.Text;
using Goro.Reading.Bytes;

namespace Goro.Reading.Tags;

/// <summary>
/// Finds the items of an APE tag of either revision, by its footer or, at the start of a file, by
/// its header. Item headers and keys are read here; values are not.
/// </summary>
public static class ApeIndex
{
    private const int FooterLength = 32;
    private const uint HasHeader = 0x80000000;
    private const uint IsHeader = 0x20000000;

    /// <summary>
    /// The tag whose footer ends at <paramref name="end"/>, or null when there is no APE footer there.
    /// The tag must start no earlier than <paramref name="floor"/>, where whatever comes before it ends.
    /// </summary>
    public static TagRead? TryReadFromFooter(BoundedReader reader, long end, long floor)
    {
        var footerAt = end - FooterLength;
        if (footerAt < floor || !TryReadPreamble(reader, footerAt, out var footer) || (footer.Flags & IsHeader) != 0)
        {
            return null;
        }

        var kind = footer.Kind;
        var itemsStart = end - footer.Size;
        var tagStart = (footer.Flags & HasHeader) != 0 ? itemsStart - FooterLength : itemsStart;
        if (footer.Size < FooterLength || tagStart < floor)
        {
            return TagRead.Unusable(kind, new Region(footerAt, FooterLength), TagUnusableReason.SizeOutsideFile, sizeTrusted: false);
        }

        return Items(reader, kind, Region.Between(tagStart, end), itemsStart, footerAt, footer.Count);
    }

    /// <summary>The tag whose header is at <paramref name="at"/>, or null when there is no APE header there.</summary>
    public static TagRead? TryReadFromHeader(BoundedReader reader, long at)
    {
        if (!TryReadPreamble(reader, at, out var header) || (header.Flags & IsHeader) == 0)
        {
            return null;
        }

        var footerEnd = at + FooterLength + header.Size;
        if (footerEnd > reader.Length)
        {
            return TagRead.Unusable(header.Kind, new Region(at, FooterLength), TagUnusableReason.SizeOutsideFile, sizeTrusted: false);
        }

        return TryReadFromFooter(reader, footerEnd, floor: at) is { Tag.Region.Start: var start } read && start == at
            ? read
            : TagRead.Unusable(header.Kind, Region.Between(at, footerEnd), TagUnusableReason.NoFooter, sizeTrusted: true);
    }

    private static TagRead Items(BoundedReader reader, TagKind kind, Region region, long itemsStart, long itemsEnd, uint count)
    {
        var fields = ImmutableArray.CreateBuilder<FieldEntry>();
        var conditions = ImmutableArray.CreateBuilder<Condition>();
        var pos = itemsStart;
        for (var i = 0u; i < count; i++)
        {
            if (pos + 8 > itemsEnd)
            {
                return Broken(pos, StructureBreak.ItemCountTooHigh);
            }

            // An item header, then a key of 2 to 255 characters and its terminator.
            if (!reader.TryRead(ReadPurpose.DeclaredStructure, pos, Math.Min(itemsEnd - pos, 8 + 256), out var headerBytes))
            {
                return Broken(pos, StructureBreak.ReadLimitReached);
            }

            var header = headerBytes.Span;
            var keyLength = header[8..].IndexOf((byte)0);
            if (keyLength < 0)
            {
                return Broken(pos, StructureBreak.ItemKeyUnterminated);
            }

            long valueSize = Binary.U32LittleEndian(header);
            var flags = Binary.U32LittleEndian(header[4..]);
            var key = Encoding.Latin1.GetString(header.Slice(8, keyLength));
            var valueStart = pos + 8 + keyLength + 1;
            var form = FormOf(kind, flags);
            if (valueSize > itemsEnd - valueStart)
            {
                fields.Add(new FieldEntry(key, form, new StoredBytes.InFile(Region.Between(valueStart, itemsEnd)), RunsPastTag: true));
                return Broken(pos, StructureBreak.FieldRunsPastTag);
            }

            fields.Add(new FieldEntry(key, form, new StoredBytes.InFile(new Region(valueStart, valueSize))));
            pos = valueStart + valueSize;
        }

        return new TagRead(new TagIndex(kind, region, TagState.Intact, fields.ToImmutable()), conditions.ToImmutable(), SizeTrusted: true);

        TagRead Broken(long at, StructureBreak reason)
        {
            conditions.Add(new TagStructureBroken(kind, at, reason));
            return new TagRead(new TagIndex(kind, region, TagState.BrokenOff, fields.ToImmutable()), conditions.ToImmutable(), SizeTrusted: true);
        }
    }

    /// <summary>
    /// An item's form. APEv1 has no item flags worth reading, and all of its items are text; APEv2
    /// marks an item as text, binary, an external locator (text, a URL) or the reserved kind.
    /// </summary>
    private static FieldForm FormOf(TagKind kind, uint flags) =>
        kind.Revision == 1
            ? FieldForm.ApeLatin1Text
            : ((flags >> 1) & 3) switch
            {
                0 or 2 => FieldForm.ApeUtf8Text,
                _ => FieldForm.ApeBinary,
            };

    private readonly record struct Preamble(TagKind Kind, uint Size, uint Count, uint Flags);

    /// <summary>A header or footer: the two have the same layout, told apart by a flag.</summary>
    private static bool TryReadPreamble(BoundedReader reader, long at, out Preamble preamble)
    {
        preamble = default;
        if (!reader.TryRead(ReadPurpose.Sniff, at, FooterLength, out var bytes) || !bytes.Span.StartsWith("APETAGEX"u8))
        {
            return false;
        }

        var span = bytes.Span;
        var version = Binary.U32LittleEndian(span[8..]);
        preamble = new Preamble(
            new TagKind(TagFormat.Ape, version >= 2000 ? 2 : 1),
            Binary.U32LittleEndian(span[12..]),
            Binary.U32LittleEndian(span[16..]),
            Binary.U32LittleEndian(span[20..]));
        return true;
    }
}
