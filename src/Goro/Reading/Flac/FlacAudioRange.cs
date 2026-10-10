using System.Collections.Immutable;
using Goro.Reading.Bytes;

namespace Goro.Reading.Flac;

/// <summary>What a frame failed, as the walk over the audio finds it.</summary>
public enum FlacFrameFaultKind
{
    /// <summary>The frame's CRC-16 does not match what it holds.</summary>
    Crc16Fails,

    /// <summary>Where the next frame's header should be, a header whose fields are well formed and whose CRC-8 fails.</summary>
    HeaderCrcFails,

    /// <summary>The frames numbered from the expected one up to the next found are missing: their headers could not be read.</summary>
    NumbersSkipped,

    /// <summary>The last frame runs past the end of the audio: it was cut off.</summary>
    CutOff,

    /// <summary>The last frame's body holds what the format forbids, so where it ends cannot be known.</summary>
    BodyMalformed,
}

/// <param name="At">Where the frame, or the header, starts.</param>
/// <param name="Number">Its frame or sample number; for <see cref="FlacFrameFaultKind.NumbersSkipped"/>, the first one missing.</param>
/// <param name="To">For <see cref="FlacFrameFaultKind.NumbersSkipped"/>, the number the walk went on from.</param>
public sealed record FlacFrameFault(FlacFrameFaultKind Kind, long At, ulong Number, ulong? To = null);

/// <summary>The range <c>goro hash</c> hashes, and every fault the walk to find it met.</summary>
public sealed record FlacAudioWalk(Region Range, ImmutableArray<FlacFrameFault> Faults);

/// <summary>
/// The range of a FLAC file that <c>goro hash</c> hashes: <c>[D, E)</c> in docs/design/flac-layout.md,
/// from the first frame to the end of the last whole one (docs/commands/hash.md), found by walking
/// every frame and checking each one's checksums on the way (docs/implementation.md, "Finding where
/// a FLAC file's audio ends").
/// </summary>
/// <remarks>
/// A frame records no length, so it ends where the next starts: the next header, by its sync code
/// and CRC-8, with the stream's blocking strategy, channels, sample rate and bit depth, and the
/// number that follows. Over damage the walk resynchronises on a later number, so that damaged
/// frames stay inside the range. The last frame, with no header after it, ends where its body says.
/// The walk reads the whole of the audio, which hashing reads anyway, so it is not held to the
/// analysis budgets.
/// </remarks>
public static class FlacAudioRange
{
    /// <summary>The least a frame can be: a header, one subframe byte, and the CRC-16.</summary>
    private const int MinFrameLength = 9;

    /// <summary>How much is searched at a time for the next header.</summary>
    private const int SearchStep = 64 * 1024;

    /// <summary>The range to hash, and the faults met, for a layout whose audio was found.</summary>
    /// <exception cref="ArgumentException">The layout's audio was not found.</exception>
    /// <exception cref="IOException">The file cannot be read, or changed while it was read.</exception>
    public static FlacAudioWalk Find(IByteSource source, FileLayout layout)
    {
        if (layout.Audio is not AudioLocation.Found(var firstFrame, var end))
        {
            throw new ArgumentException("There is no audio to find the range of.", nameof(layout));
        }

        var window = new SequentialWindow(source, end);
        var faults = ImmutableArray.CreateBuilder<FlacFrameFault>();
        if (!TryHeader(window, firstFrame, end, out var first, out var firstCrc) || !firstCrc)
        {
            return new FlacAudioWalk(Region.Between(firstFrame, firstFrame), []);
        }

        var depth = first.BitDepth != 0 ? first.BitDepth : (layout.Details as FormatDetails.Flac)?.StreamInfo?.BitDepth ?? 0;
        var damaged = new List<Region>();
        var at = firstFrame;
        var current = first;
        while (FindNext(window, at, current, first, end) is var (next, header))
        {
            var expected = Expected(current);
            var skipped = header.Number != expected;
            if (skipped)
            {
                faults.Add(new FlacFrameFault(FlacFrameFaultKind.NumbersSkipped, next, expected, header.Number));
            }

            if (!FrameCrcPasses(window, at, next))
            {
                faults.Add(new FlacFrameFault(FlacFrameFaultKind.Crc16Fails, at, current.Number));
                damaged.Add(Region.Between(at, next));
            }
            else if (skipped)
            {
                damaged.Add(Region.Between(at, next));
            }

            at = next;
            current = header;
        }

        var stop = LastFrameEnd(window, at, current, end, depth, faults);
        if (stop < end)
        {
            damaged.Add(Region.Between(Math.Max(stop, at + current.Length), end));
        }

        // Where the walk knows something is wrong, a damaged header shows as one whose fields are
        // well formed and whose CRC-8 fails. Looked for there only, since such a run can occur by
        // chance anywhere in healthy audio.
        foreach (var region in damaged)
        {
            DamagedHeaders(window, region, first, faults);
        }

        return new FlacAudioWalk(Region.Between(firstFrame, stop), [.. faults.OrderBy(f => f.At)]);
    }

    private static ulong Expected(FlacFrameHeader header) => header.VariableBlocking ? header.Number + (ulong)header.BlockSize : header.Number + 1;

    /// <summary>
    /// The next frame after the one at <paramref name="at"/>: the first header of the stream after it
    /// numbered as the next frame, or later, as far as the frames skipped could fit in the bytes
    /// between.
    /// </summary>
    private static (long At, FlacFrameHeader Header)? FindNext(SequentialWindow window, long at, FlacFrameHeader current, FlacFrameHeader stream, long end)
    {
        var expected = Expected(current);
        for (var from = at + current.Length + 1; from < end; from += SearchStep)
        {
            // Each chunk runs a header's length into the next, so a header across the boundary is
            // parsed from the chunk itself: the window's buffer is reused when it moves on.
            var length = (int)Math.Min(SearchStep + FlacFrameHeader.MaxLength, end - from);
            var scan = Math.Min(SearchStep, length);
            if (!window.TryGet(from, length, out var chunk))
            {
                return null;
            }

            for (var i = chunk[..scan].IndexOf((byte)0xFF); i >= 0; i = Next(chunk[..scan], i))
            {
                var position = from + i;
                if (!FlacFrameHeader.TryParseIgnoringCrc(chunk[i..], out var header, out var crcPasses) || !SameStream(header, stream))
                {
                    continue;
                }

                // As many frames could have been lost as fit between the two headers.
                var mostSkipped = (ulong)((position - at) / MinFrameLength + 1) * (header.VariableBlocking ? (ulong)Math.Max(1, stream.BlockSize) : 1UL);
                if (header.Number < expected || header.Number - expected > mostSkipped)
                {
                    continue;
                }

                if (crcPasses)
                {
                    return (position, header);
                }
            }
        }

        return null;

        static int Next(ReadOnlySpan<byte> chunk, int i)
        {
            var next = chunk[(i + 1)..].IndexOf((byte)0xFF);
            return next < 0 ? -1 : i + 1 + next;
        }
    }

    /// <summary>Every header of the stream in <paramref name="region"/> whose fields are well formed and whose CRC-8 fails.</summary>
    private static void DamagedHeaders(SequentialWindow window, Region region, FlacFrameHeader stream, ImmutableArray<FlacFrameFault>.Builder faults)
    {
        for (var from = region.Start; from < region.End; from += SearchStep)
        {
            var scan = (int)Math.Min(SearchStep, region.End - from);
            var length = (int)Math.Min(scan + FlacFrameHeader.MaxLength, region.End - from + FlacFrameHeader.MaxLength);
            if (!window.TryGet(from, length, out var chunk) && !window.TryGet(from, scan, out chunk))
            {
                return;
            }

            for (var i = 0; i < Math.Min(scan, chunk.Length); i++)
            {
                if (chunk[i] == 0xFF
                    && FlacFrameHeader.TryParseIgnoringCrc(chunk[i..], out var header, out var crcPasses)
                    && !crcPasses
                    && SameStream(header, stream))
                {
                    faults.Add(new FlacFrameFault(FlacFrameFaultKind.HeaderCrcFails, from + i, header.Number));
                }
            }
        }
    }

    private static bool SameStream(FlacFrameHeader header, FlacFrameHeader stream) =>
        header.VariableBlocking == stream.VariableBlocking
        && header.Channels == stream.Channels
        && header.SampleRate == stream.SampleRate
        && header.BitDepth == stream.BitDepth;

    /// <summary>Whether the frame <c>[at, next)</c> matches its CRC-16, taken in pieces where it is longer than a block.</summary>
    private static bool FrameCrcPasses(SequentialWindow window, long at, long next)
    {
        ushort crc = 0;
        var body = next - 2;
        for (var from = at; from < body; )
        {
            var length = (int)Math.Min(SequentialWindow.BlockLength, body - from);
            if (!window.TryGet(from, length, out var piece))
            {
                return false;
            }

            crc = Checksums.Crc16(piece, crc);
            from += length;
        }

        return window.TryGet(body, 2, out var stored) && Binary.U16BigEndian(stored) == crc;
    }

    /// <summary><c>E</c>: where the last frame's body says it ends, or its start where it was cut off or cannot be read.</summary>
    private static long LastFrameEnd(SequentialWindow window, long at, FlacFrameHeader header, long end, int depth, ImmutableArray<FlacFrameFault>.Builder faults)
    {
        var length = (int)Math.Min(SequentialWindow.BlockLength, end - at);
        if (!window.TryGet(at, length, out var bytes))
        {
            return at;
        }

        switch (FlacFrameBody.FindEnd(bytes, header, depth))
        {
            case FlacFrameEnd.EndsAt(var frameLength, var crcPasses):
                if (!crcPasses)
                {
                    faults.Add(new FlacFrameFault(FlacFrameFaultKind.Crc16Fails, at, header.Number));
                }

                return at + frameLength;
            case FlacFrameEnd.RunsPast:
                faults.Add(new FlacFrameFault(FlacFrameFaultKind.CutOff, at, header.Number));
                return at;
            default:
                faults.Add(new FlacFrameFault(FlacFrameFaultKind.BodyMalformed, at, header.Number));
                return at;
        }
    }

    private static bool TryHeader(SequentialWindow window, long at, long end, out FlacFrameHeader header, out bool crcPasses)
    {
        header = default;
        crcPasses = false;
        var length = (int)Math.Min(FlacFrameHeader.MaxLength, end - at);
        return length >= 6 && window.TryGet(at, length, out var bytes) && FlacFrameHeader.TryParseIgnoringCrc(bytes, out header, out crcPasses);
    }
}
