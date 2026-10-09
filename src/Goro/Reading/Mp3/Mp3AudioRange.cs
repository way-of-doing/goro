using Goro.Reading.Bytes;

namespace Goro.Reading.Mp3;

/// <summary>
/// The range of an MP3 that <c>goro hash</c> hashes: <c>[D, E)</c> in docs/design/mp3-layout.md,
/// from the first audio frame to the end of the last whole frame, and nothing the container adds
/// around them (docs/commands/hash.md).
/// </summary>
/// <remarks>
/// <c>E</c> is found by walking every frame header from <c>D</c>, which reads the whole of the audio:
/// hashing reads it anyway, so the walk is not held to the analysis budgets. It resynchronises over
/// damage, so that bit rot in the middle of the audio stays inside the range, where it changes the
/// hash. Junk after the audio can hold chance frame headers, so a frame counts towards <c>E</c> only
/// when it ends exactly where the audio does or belongs to a run of <see cref="RunLength"/>
/// consecutive frames (docs/implementation.md, "Finding where the audio ends").
/// </remarks>
public static class Mp3AudioRange
{
    /// <summary>
    /// How many consecutive frames make a run that real audio has and junk does not. Two frames can
    /// confirm each other by chance; three in a row, each starting where the last ended, is what audio
    /// always is and junk practically never.
    /// </summary>
    public const int RunLength = 3;

    /// <summary>How far ahead the end of a free-format frame is looked for, as in <see cref="MpegFrames"/>.</summary>
    private const int FreeFormatReach = 4000;

    /// <summary>The range to hash, for a layout whose audio was found.</summary>
    /// <exception cref="ArgumentException">The layout's audio was not found.</exception>
    /// <exception cref="IOException">The file cannot be read, or changed while it was read.</exception>
    public static Region Find(IByteSource source, FileLayout layout)
    {
        if (layout.Audio is not AudioLocation.Found(var firstFrame, var trailingTagsStart))
        {
            throw new ArgumentException("There is no audio to find the range of.", nameof(layout));
        }

        // The bytes after the audio a trusted summary header describes are junk, and never hashed.
        var limit = layout.Duration is DurationOutcome.Known { Basis: DurationBasis.SummaryHeaderToFrameEnd } && layout.Summary?.ByteCount is { } bytes
            ? firstFrame + bytes
            : trailingTagsStart;

        var window = new SequentialWindow(source, limit);
        if (!window.TryGet(firstFrame, MpegFrameHeader.Length, out var firstBytes) || !MpegFrameHeader.TryParse(firstBytes, out var stream))
        {
            return Region.Between(firstFrame, firstFrame);
        }

        // D: past the summary frame, which belongs to the container.
        var start = layout.Summary is not null && FrameLength(window, firstFrame, stream, previous: 0, limit) is var summaryLength and > 0
            ? firstFrame + summaryLength
            : firstFrame;
        return Region.Between(start, End(window, start, limit, stream));
    }

    /// <summary><c>E</c>: the largest end of a frame that ends at <paramref name="limit"/> or belongs to a run.</summary>
    private static long End(SequentialWindow window, long start, long limit, MpegFrameHeader stream)
    {
        long? lastEnd = null;
        long? qualified = null;
        var lastLength = 0;
        var run = 0;
        var pos = start;
        while (pos + MpegFrameHeader.Length <= limit && window.TryGet(pos, MpegFrameHeader.Length, out var bytes))
        {
            if (bytes[0] == 0xFF && MpegFrameHeader.TryParse(bytes, out var header) && header.Matches(stream)
                && FrameLength(window, pos, header, previous: lastEnd == pos ? lastLength : 0, limit) is var length and > 0
                && pos + length <= limit)
            {
                lastLength = length;
                run = lastEnd == pos ? run + 1 : 1;
                lastEnd = pos + length;
                if (run >= RunLength || lastEnd == limit)
                {
                    qualified = lastEnd;
                }

                pos = lastEnd.Value;
                continue;
            }

            // Not a frame here: damage, or junk. Look for the next one a byte further on.
            pos++;
        }

        // A stream too short to make a run, and not ending at the limit: the last frame found.
        return qualified ?? lastEnd ?? start;
    }

    /// <summary>
    /// A frame's length: its header's, or for free format the distance to the next header with the same
    /// fixed bits. The last free-format frame has no next header to measure by, so it takes the length
    /// of the frame before it, <paramref name="previous"/>, where that ends at the limit, padding aside.
    /// </summary>
    private static int FrameLength(SequentialWindow window, long at, MpegFrameHeader header, int previous, long limit)
    {
        if (!header.IsFreeFormat)
        {
            return header.FrameLength;
        }

        if (!window.TryGet(at, MpegFrameHeader.Length, out var first))
        {
            return 0;
        }

        var (b0, b1, b2) = (first[0], first[1], first[2] & 0xFC);
        for (var i = MpegFrameHeader.Length; i <= FreeFormatReach; i++)
        {
            if (!window.TryGet(at + i, MpegFrameHeader.Length, out var next))
            {
                return previous > 0 && Math.Abs(limit - at - previous) <= 1 ? (int)(limit - at) : 0;
            }

            // The padding bit may differ from frame to frame, so it is left out of the comparison.
            if (next[0] == b0 && next[1] == b1 && (next[2] & 0xFC) == b2 && MpegFrameHeader.TryParse(next, out _))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// The bytes of the audio, read forwards in large blocks: a walk only moves forwards, so a block
    /// is read once, and a read that runs past one starts the next where it is.
    /// </summary>
    private sealed class SequentialWindow(IByteSource source, long end)
    {
        private const int BlockLength = 1024 * 1024;

        private readonly byte[] block = new byte[BlockLength];
        private long blockStart;
        private int blockLength;

        public bool TryGet(long at, int count, out ReadOnlySpan<byte> bytes)
        {
            bytes = default;
            if (at < 0 || at + count > end || count > BlockLength)
            {
                return false;
            }

            if (at < blockStart || at + count > blockStart + blockLength)
            {
                blockStart = at;
                blockLength = (int)Math.Min(BlockLength, end - at);
                if (source.Read(at, block.AsSpan(0, blockLength)) != blockLength)
                {
                    throw new IOException("The file changed while it was being read.");
                }
            }

            bytes = block.AsSpan((int)(at - blockStart), count);
            return true;
        }
    }
}
