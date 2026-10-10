using System.Collections.Immutable;
using Goro.Reading.Bytes;
using Goro.Reading.Tags;

namespace Goro.Reading.Flac;

/// <summary>The metadata block types RFC 9639 defines (section 8.1); 7 to 126 are reserved, 127 forbidden.</summary>
public enum FlacBlockType
{
    StreamInfo = 0,
    Padding = 1,
    Application = 2,
    SeekTable = 3,
    VorbisComment = 4,
    CueSheet = 5,
    Picture = 6,
}

/// <summary>
/// One metadata block: where its header is, the region it was decided to occupy, and the length its
/// header recorded, which differs from that region's where the length was wrong and the block's
/// own contents gave its end (docs/design/quirks.md).
/// </summary>
/// <param name="Comments">For a VORBIS_COMMENT block, its comment list as the list's own lengths describe it.</param>
public sealed record FlacBlock(FlacBlockType Type, long At, Region Payload, long RecordedLength, bool Last, VorbisCommentList? Comments = null)
{
    public bool LengthRecovered => Payload.Length != RecordedLength;
}

/// <summary>
/// The fields of a STREAMINFO block (RFC 9639, section 8.2). A block shorter than 34 bytes but at
/// least 18 holds every field but the MD5.
/// </summary>
public sealed record StreamInfo(
    int MinBlockSize, int MaxBlockSize, int MinFrameSize, int MaxFrameSize, int SampleRate, int Channels, int BitDepth, ulong TotalSamples)
{
    public const int Length = 34;

    /// <summary>How much of the block holds the fields above.</summary>
    public const int FieldsLength = 18;

    public static StreamInfo? TryParse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < FieldsLength)
        {
            return null;
        }

        var packed = ((ulong)Binary.U32BigEndian(payload[10..]) << 32) | Binary.U32BigEndian(payload[14..]);
        return new StreamInfo(
            Binary.U16BigEndian(payload),
            Binary.U16BigEndian(payload[2..]),
            (int)Binary.U24BigEndian(payload[4..]),
            (int)Binary.U24BigEndian(payload[7..]),
            (int)(packed >> 44),
            (int)((packed >> 41) & 7) + 1,
            (int)((packed >> 36) & 31) + 1,
            packed & 0xF_FFFF_FFFF);
    }
}

/// <summary>
/// A FLAC file's metadata as its block chain gives it: the blocks, and <see cref="End"/>, <c>K</c> of
/// docs/design/flac-layout.md, where the first frame starts. Where the chain is lost,
/// <see cref="End"/> is null and <see cref="LostAt"/> says where; the first frame is then for the
/// caller to search for.
/// </summary>
public sealed record FlacMetadata(ImmutableArray<FlacBlock> Blocks, long? End, long LostAt, StreamInfo? StreamInfo, ImmutableArray<Condition> Conditions)
{
    /// <summary>
    /// How many blocks a chain may hold before it is taken for one read from the wrong place. Four
    /// zero bytes read as a block header make an empty block, so a chain read into padding can run
    /// for a long time; no file needs more than a few dozen.
    /// </summary>
    public const int MaxBlocks = 4096;

    /// <summary>
    /// Walks the chain from the first block header, at <paramref name="start"/>, just after the
    /// <c>fLaC</c> marker. Where a block that states its own extent disagrees with its recorded
    /// length, the end from which the rest of the chain reads cleanly to the first frame wins.
    /// </summary>
    public static FlacMetadata Read(BoundedReader reader, long start)
    {
        var blocks = ImmutableArray.CreateBuilder<FlacBlock>();
        var conditions = new List<Condition>();
        StreamInfo? streamInfo = null;
        var at = start;
        while (true)
        {
            if (blocks.Count >= MaxBlocks)
            {
                return Lost(at, MetadataChainBreak.InvalidBlockHeader);
            }

            if (ReadHeader(reader, at) is not { } header)
            {
                return Lost(at, at + 4 > reader.Length ? MetadataChainBreak.BlockRunsPastFile : MetadataChainBreak.InvalidBlockHeader);
            }

            if (header.SyncByte && blocks.Count > 0 && IsFirstFrame(reader, at))
            {
                // The frame's sync code reads as a block of type 127: no block was flagged last.
                conditions.Add(new FlacQuirkApplied(FlacQuirk.NoLastBlockFlag, at));
                return Done(at);
            }

            if (header.Type == 127 || (header.Type == 0 && blocks.Count > 0))
            {
                return Lost(at, header.Type == 0 ? MetadataChainBreak.SecondStreamInfo : MetadataChainBreak.InvalidBlockHeader);
            }

            var payloadStart = at + 4;
            var recordedEnd = payloadStart + header.Length;
            VorbisCommentList? comments = null;
            long? contentsEnd = (FlacBlockType)header.Type switch
            {
                FlacBlockType.StreamInfo => payloadStart + StreamInfo.Length,
                FlacBlockType.Picture => PictureEnd(reader, payloadStart),
                FlacBlockType.VorbisComment => (comments = VorbisCommentIndex.Read(reader, payloadStart, reader.Length)).End,
                _ => null,
            };

            var end = recordedEnd;
            if (contentsEnd is { } contents && contents != recordedEnd)
            {
                if (ReadsOnCleanly(reader, recordedEnd, header.Last))
                {
                    end = recordedEnd;
                }
                else if (ReadsOnCleanly(reader, contents, header.Last))
                {
                    end = contents;
                    conditions.Add(new FlacQuirkApplied(FlacQuirk.BlockLengthRecovered, at));
                }
                else
                {
                    return Lost(at, recordedEnd > reader.Length ? MetadataChainBreak.BlockRunsPastFile : MetadataChainBreak.InvalidBlockHeader);
                }
            }
            else if (recordedEnd > reader.Length)
            {
                return Lost(at, MetadataChainBreak.BlockRunsPastFile);
            }

            var type = (FlacBlockType)header.Type;
            var payload = Region.Between(payloadStart, end);
            blocks.Add(new FlacBlock(type, at, payload, header.Length, header.Last, comments));
            if (type == FlacBlockType.StreamInfo && blocks.Count == 1)
            {
                streamInfo = ReadStreamInfo(reader, payload, conditions);
            }

            if (header.Last)
            {
                return IsFirstFrame(reader, end) ? Done(end) : Lost(end, MetadataChainBreak.NoFrameAfterLastBlock);
            }

            at = end;
        }

        FlacMetadata Done(long end) => new(blocks.ToImmutable(), end, end, streamInfo, [.. conditions]);

        FlacMetadata Lost(long where, MetadataChainBreak why)
        {
            conditions.Add(new MetadataChainLost(where, why));
            return new FlacMetadata(blocks.ToImmutable(), null, where, streamInfo, [.. conditions]);
        }
    }

    /// <param name="SyncByte">The header's first byte is <c>FF</c>, as a frame's first byte is.</param>
    private readonly record struct BlockHeader(int Type, long Length, bool Last, bool SyncByte);

    private static BlockHeader? ReadHeader(BoundedReader reader, long at) =>
        reader.TryRead(ReadPurpose.DeclaredStructure, at, 4, out var bytes)
            ? new BlockHeader(bytes.Span[0] & 0x7F, Binary.U24BigEndian(bytes.Span[1..]), (bytes.Span[0] & 0x80) != 0, bytes.Span[0] == 0xFF)
            : null;

    /// <summary>
    /// Whether the chain read from <paramref name="at"/> reaches the first frame cleanly: a frame
    /// numbered 0 right there if the block before was flagged last, or else well-formed blocks of the
    /// types after the first, the last of them flagged and followed by that frame.
    /// </summary>
    private static bool ReadsOnCleanly(BoundedReader reader, long at, bool previousWasLast)
    {
        if (previousWasLast)
        {
            return IsFirstFrame(reader, at);
        }

        for (var steps = 0; steps < MaxBlocks; steps++)
        {
            if (ReadHeader(reader, at) is not { } header)
            {
                return false;
            }

            if (header.SyncByte)
            {
                return IsFirstFrame(reader, at);
            }

            if (header.Type is 0 or >= 127 || at + 4 + header.Length > reader.Length)
            {
                return false;
            }

            at += 4 + header.Length;
            if (header.Last)
            {
                return IsFirstFrame(reader, at);
            }
        }

        return false;
    }

    /// <summary>Whether a frame header numbered 0 starts at <paramref name="at"/>: where the audio begins.</summary>
    public static bool IsFirstFrame(BoundedReader reader, long at)
    {
        var length = Math.Min(FlacFrameHeader.MaxLength, reader.Length - at);
        return length >= 6
            && reader.TryRead(ReadPurpose.Sniff, at, length, out var bytes)
            && bytes.Span[0] == 0xFF
            && FlacFrameHeader.TryParse(bytes.Span, out var header)
            && header.Number == 0;
    }

    /// <summary>Where a PICTURE block's contents say it ends: after its media type, description, four sizes and data.</summary>
    private static long? PictureEnd(BoundedReader reader, long start)
    {
        var at = start + 4;
        foreach (var skipAfter in (long[])[0, 16])
        {
            if (!reader.TryRead(ReadPurpose.DeclaredStructure, at, 4, out var length))
            {
                return null;
            }

            at += 4 + Binary.U32BigEndian(length.Span) + skipAfter;
        }

        return reader.TryRead(ReadPurpose.DeclaredStructure, at, 4, out var data) ? at + 4 + Binary.U32BigEndian(data.Span) : null;
    }

    private static StreamInfo? ReadStreamInfo(BoundedReader reader, Region payload, List<Condition> conditions)
    {
        var length = Math.Min(payload.Length, StreamInfo.Length);
        if (!reader.TryRead(ReadPurpose.DeclaredStructure, payload.Start, length, out var bytes))
        {
            return null;
        }

        if (length < StreamInfo.Length)
        {
            conditions.Add(new StreamInfoShort((int)length));
        }

        return StreamInfo.TryParse(bytes.Span);
    }
}
