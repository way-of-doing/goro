using System.Collections.Immutable;
using System.Text;
using Goro.Reading.Bytes;

namespace Goro.Reading.Tags;

/// <summary>
/// A tag as its index found it, with the conditions met on the way. <see cref="SizeTrusted"/> says
/// whether the tag's declared size can be used to step over it: false when the size itself is what
/// is damaged, so that whatever follows has to be found some other way.
/// </summary>
public sealed record TagRead(TagIndex Tag, ImmutableArray<Condition> Conditions, bool SizeTrusted)
{
    internal static TagRead Unusable(TagKind kind, Region region, TagUnusableReason reason, bool sizeTrusted) =>
        new(TagIndex.Unusable(kind, region), [new TagUnusable(kind, region, reason)], sizeTrusted);
}

/// <summary>
/// Finds the frames of an Id3v2 tag of any revision, reading frame headers and nothing else: no
/// frame's content is read here (docs/design/file-reading.md, "Tag values are read on demand").
/// </summary>
public static class Id3v2Index
{
    private const int HeaderLength = 10;

    /// <summary>The tag whose header is at <paramref name="at"/>, or null when there is no Id3v2 header there.</summary>
    public static TagRead? TryRead(BoundedReader reader, long at)
    {
        if (!reader.TryRead(ReadPurpose.Sniff, at, HeaderLength, out var headerBytes) || !headerBytes.Span.StartsWith("ID3"u8))
        {
            return null;
        }

        var header = headerBytes.Span;
        var major = header[3];
        var flags = header[5];
        var kind = new TagKind(TagFormat.Id3v2, major);
        var headerRegion = new Region(at, HeaderLength);
        if (major is < 2 or > 4)
        {
            return TagRead.Unusable(kind, headerRegion, TagUnusableReason.UnknownVersion, sizeTrusted: false);
        }

        if (!Binary.IsSyncsafe(header[6..]))
        {
            return TagRead.Unusable(kind, headerRegion, TagUnusableReason.SizeNotSyncsafe, sizeTrusted: false);
        }

        long size = Binary.Syncsafe(header[6..]);
        var footer = major == 4 && (flags & 0x10) != 0 ? HeaderLength : 0;
        var region = new Region(at, HeaderLength + size + footer);
        if (region.End > reader.Length)
        {
            return TagRead.Unusable(kind, headerRegion, TagUnusableReason.SizeOutsideFile, sizeTrusted: false);
        }

        if (major == 2 && (flags & 0x40) != 0)
        {
            return TagRead.Unusable(kind, region, TagUnusableReason.UndefinedCompression, sizeTrusted: true);
        }

        // Before v2.4, unsynchronisation applies to the tag as a whole, and frame boundaries can only
        // be found once it is undone; from v2.4 it applies frame by frame.
        TagBody body;
        var bodyStart = at + HeaderLength;
        if ((flags & 0x80) != 0 && major < 4)
        {
            if (!reader.TryRead(ReadPurpose.DeclaredStructure, bodyStart, size, out var stored))
            {
                return TagRead.Unusable(kind, region, TagUnusableReason.TooLargeToResynchronise, sizeTrusted: true);
            }

            body = new MemoryBody(bodyStart, Binary.Resynchronise(stored.Span));
        }
        else
        {
            body = new FileBody(reader, bodyStart, size);
        }

        var walk = new FrameWalk(kind, body, tagUnsynchronised: major == 4 && (flags & 0x80) != 0);
        walk.Run(extendedHeader: (flags & 0x40) != 0 && major >= 3);
        var state = walk.Broken ? TagState.BrokenOff : TagState.Intact;
        return new TagRead(new TagIndex(kind, region, state, walk.Fields.ToImmutable()), walk.Conditions.ToImmutable(), SizeTrusted: true);
    }

    /// <summary>Walks the frame headers of one tag body.</summary>
    private sealed class FrameWalk(TagKind kind, TagBody body, bool tagUnsynchronised)
    {
        private readonly int major = kind.Revision;
        private readonly int idLength = kind.Revision == 2 ? 3 : 4;
        private readonly int headerLength = kind.Revision == 2 ? 6 : 10;

        public ImmutableArray<FieldEntry>.Builder Fields { get; } = ImmutableArray.CreateBuilder<FieldEntry>();

        public ImmutableArray<Condition>.Builder Conditions { get; } = ImmutableArray.CreateBuilder<Condition>();

        public bool Broken { get; private set; }

        public void Run(bool extendedHeader)
        {
            long pos = 0;
            if (extendedHeader)
            {
                if (!body.TryRead(0, 4, out var size))
                {
                    Break(0, StructureBreak.ExtendedHeaderRunsPastTag);
                    return;
                }

                // v2.3's size leaves itself out; v2.4's is syncsafe and counts itself.
                var length = major == 3 ? Binary.U32BigEndian(size.Span) + 4L : Binary.Syncsafe(size.Span);
                if (length < 6 || length > body.Length)
                {
                    Break(0, StructureBreak.ExtendedHeaderRunsPastTag);
                    return;
                }

                pos = length;
            }

            // Frame sizes in v2.4 are syncsafe, but iTunes once wrote plain integers. That is decided
            // once for the tag, as TagLib and mutagen decide it: plain if walking syncsafe sizes does
            // not land on frame boundaries and walking plain ones does (docs/design/quirks.md).
            var syncsafe = major == 4;
            if (major == 4 && !WalksCleanly(pos, syncsafe: true) && WalksCleanly(pos, syncsafe: false))
            {
                syncsafe = false;
                Conditions.Add(new QuirkApplied(kind, TagQuirk.PlainFrameSizes, body.OffsetOf(pos)));
            }

            while (pos + headerLength <= body.Length)
            {
                if (!body.TryRead(pos, headerLength, out var headerBytes))
                {
                    Break(pos, StructureBreak.ReadLimitReached);
                    return;
                }

                var header = headerBytes.Span;
                if (header[0] == 0)
                {
                    if (HasDataAfter(pos))
                    {
                        Break(pos, StructureBreak.DataAfterPadding);
                    }

                    return;
                }

                if (!IsFrameId(header[..idLength]))
                {
                    // An illegal identifier whose size lands on another frame, the padding or the end
                    // of the tag is stepped over, which is what the size is for (docs/design/quirks.md).
                    if (major > 2 && StepsCleanly(pos, syncsafe) is { } skipped)
                    {
                        Conditions.Add(new QuirkApplied(kind, TagQuirk.IllegalFrameIdentifierSteppedOver, body.OffsetOf(pos)));
                        pos += skipped;
                        continue;
                    }

                    Break(pos, StructureBreak.NoFrameIdentifier);
                    return;
                }

                var key = Id3v2FrameNames.Canonical(Encoding.ASCII.GetString(header[..idLength]));
                var form = Id3v2FrameNames.FormOf(key);
                long size = major == 2 ? Binary.U24BigEndian(header[3..]) : syncsafe ? Binary.Syncsafe(header[4..]) : Binary.U32BigEndian(header[4..]);
                var contentStart = pos + headerLength;
                if (size > body.Length - contentStart)
                {
                    Fields.Add(new FieldEntry(key, form, body.Stored(contentStart, body.Length - contentStart), RunsPastTag: true));
                    Break(pos, StructureBreak.FieldRunsPastTag);
                    return;
                }

                var flags = major == 2 ? (ushort)0 : Binary.U16BigEndian(header[8..]);
                Fields.Add(new FieldEntry(key, form, body.Stored(contentStart, size), TransformOf(flags)));
                pos = contentStart + size;
            }
        }

        private void Break(long pos, StructureBreak reason)
        {
            Broken = true;
            Conditions.Add(new TagStructureBroken(kind, body.OffsetOf(pos), reason));
        }

        private FieldTransform TransformOf(ushort flags) => major switch
        {
            3 => new FieldTransform(
                PrefixLength: ((flags & 0x0080) != 0 ? 4 : 0) + ((flags & 0x0040) != 0 ? 1 : 0) + ((flags & 0x0020) != 0 ? 1 : 0),
                Unsynchronised: false,
                Encrypted: (flags & 0x0040) != 0,
                Compressed: (flags & 0x0080) != 0),
            4 => new FieldTransform(
                PrefixLength: ((flags & 0x0040) != 0 ? 1 : 0) + ((flags & 0x0004) != 0 ? 1 : 0) + ((flags & 0x0001) != 0 ? 4 : 0),
                Unsynchronised: (flags & 0x0002) != 0 || tagUnsynchronised,
                Encrypted: (flags & 0x0004) != 0,
                Compressed: (flags & 0x0008) != 0),
            _ => FieldTransform.None,
        };

        private static bool IsFrameId(ReadOnlySpan<byte> id)
        {
            foreach (var b in id)
            {
                if (b is not (>= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9'))
                {
                    return false;
                }
            }

            return true;
        }

        private long Size(ReadOnlySpan<byte> header, bool syncsafe) =>
            syncsafe ? Binary.Syncsafe(header[4..]) : Binary.U32BigEndian(header[4..]);

        /// <summary>Whether frame sizes, read one way, lead from frame to frame to the padding or the end of the tag.</summary>
        private bool WalksCleanly(long start, bool syncsafe)
        {
            var pos = start;
            var frames = 0;
            while (pos + 10 <= body.Length)
            {
                if (!body.TryRead(pos, 10, out var header))
                {
                    return false;
                }

                if (header.Span[0] == 0)
                {
                    return frames > 0;
                }

                if (!IsFrameId(header.Span[..4]))
                {
                    return false;
                }

                pos += 10 + Size(header.Span, syncsafe);
                frames++;
            }

            return pos == body.Length;
        }

        /// <summary>
        /// The length, header included, of a frame with a printable but illegal identifier, when its
        /// size lands on another frame, the padding or the end of the tag; otherwise null.
        /// </summary>
        private long? StepsCleanly(long pos, bool syncsafe)
        {
            if (pos + 10 > body.Length || !body.TryRead(pos, 10, out var header) || header.Span[..4].ContainsAnyExceptInRange((byte)0x20, (byte)0x7E))
            {
                return null;
            }

            var length = 10 + Size(header.Span, syncsafe);
            var next = pos + length;
            if (next == body.Length)
            {
                return length;
            }

            return next < body.Length && body.TryRead(next, Math.Min(4, body.Length - next), out var following)
                && (following.Span[0] == 0 || following.Length == 4 && IsFrameId(following.Span))
                ? length
                : null;
        }

        /// <summary>
        /// Whether anything but zero follows the start of the padding, which means a block of the tag
        /// was zeroed rather than the frames having ended. Padding more than the budget allows to check
        /// is taken as padding, without reading any of it.
        /// </summary>
        private bool HasDataAfter(long pos)
        {
            if (!body.CanRead(pos, body.Length - pos))
            {
                return false;
            }

            const int chunk = 64 * 1024;
            for (var at = pos; at < body.Length; at += chunk)
            {
                if (!body.TryRead(at, Math.Min(chunk, body.Length - at), out var bytes))
                {
                    return false;
                }

                if (bytes.Span.ContainsAnyExcept((byte)0))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The bytes of a tag after its header, wherever they are.</summary>
    private abstract class TagBody(long fileOffset)
    {
        /// <summary>Where the body starts in the file.</summary>
        protected long FileOffset { get; } = fileOffset;

        public abstract long Length { get; }

        public abstract bool TryRead(long pos, long count, out ReadOnlyMemory<byte> bytes);

        /// <summary>Whether a read would be served, without making it.</summary>
        public abstract bool CanRead(long pos, long count);

        public abstract StoredBytes Stored(long pos, long length);

        /// <summary>Where in the file a position in the body is, as nearly as can be said.</summary>
        public long OffsetOf(long pos) => FileOffset + pos;
    }

    /// <summary>A body read from the file as it is needed.</summary>
    private sealed class FileBody(BoundedReader reader, long start, long length) : TagBody(start)
    {
        public override long Length => length;

        public override bool TryRead(long pos, long count, out ReadOnlyMemory<byte> bytes) =>
            reader.TryRead(ReadPurpose.DeclaredStructure, FileOffset + pos, count, out bytes);

        // The padding is read in chunks, so the whole of it is asked about at once.
        public override bool CanRead(long pos, long count) =>
            reader.CanRead(ReadPurpose.DeclaredStructure, FileOffset + pos, count);

        public override StoredBytes Stored(long pos, long count) => new StoredBytes.InFile(new Region(FileOffset + pos, count));
    }

    /// <summary>A body resynchronised as a whole, whose positions no longer match the file's.</summary>
    private sealed class MemoryBody(long start, byte[] bytes) : TagBody(start)
    {
        public override long Length => bytes.Length;

        public override bool TryRead(long pos, long count, out ReadOnlyMemory<byte> result)
        {
            result = default;
            if (pos < 0 || count < 0 || pos > bytes.Length - count)
            {
                return false;
            }

            result = bytes.AsMemory((int)pos, (int)count);
            return true;
        }

        public override bool CanRead(long pos, long count) => pos >= 0 && count >= 0 && pos <= bytes.Length - count;

        public override StoredBytes Stored(long pos, long count) => new StoredBytes.InMemory(bytes.AsMemory((int)pos, (int)count));
    }
}
