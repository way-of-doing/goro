using Goro.Reading.Bytes;

namespace Goro.Reading.Mp3;

/// <summary>
/// Finds MPEG audio frames whose position nothing declares. Eleven set bits are easy to come by in
/// tag data and junk, so a header counts only when another one matching it sits where it says the
/// frame ends (docs/implementation.md, "An MP3 frame is confirmed by the frames after it").
/// </summary>
public static class MpegFrames
{
    /// <summary>How far ahead the end of a free-format frame is looked for.</summary>
    private const int FreeFormatReach = 4000;

    /// <summary>
    /// How much a search reads at a time. The first frame nearly always follows the tags directly,
    /// so the search reads in steps rather than its whole limit at once.
    /// </summary>
    private const int SearchStep = 16 * 1024;

    /// <summary>
    /// The first confirmed frame in [<paramref name="from"/>, <paramref name="end"/>), searching no
    /// further than the search budget allows. <paramref name="gaveUp"/> says whether the search
    /// stopped for want of budget before reaching <paramref name="end"/>.
    /// </summary>
    public static long? FindFirst(BoundedReader reader, long from, long end, out bool gaveUp)
    {
        gaveUp = false;
        if (from >= end)
        {
            return null;
        }

        for (var at = from; at < end; at += SearchStep)
        {
            if (!reader.TryRead(ReadPurpose.Search, at, Math.Min(SearchStep, end - at), out var block))
            {
                gaveUp = true;
                return null;
            }

            var span = block.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == 0xFF && IsConfirmed(reader, at + i, end, out _))
                {
                    return at + i;
                }
            }
        }

        return null;
    }

    /// <summary>Whether a frame header at <paramref name="at"/> is confirmed by the frame that follows it.</summary>
    public static bool IsConfirmed(BoundedReader reader, long at, long end, out MpegFrameHeader header)
    {
        if (!TryHeaderAt(reader, at, end, out header))
        {
            return false;
        }

        var freeFormat = header.IsFreeFormat;
        var length = freeFormat ? FreeFormatLength(reader, at, end) : header.FrameLength;
        if (length <= 0)
        {
            return false;
        }

        header = header with { FrameLength = length };
        var next = at + length;
        if (next >= end)
        {
            // Nothing after it to confirm it: sync near the end of a tag would otherwise pass for a
            // frame, in a file that holds no audio at all.
            return false;
        }

        if (!TryHeaderAt(reader, next, end, out var following) || !following.Matches(header))
        {
            return false;
        }

        if (!freeFormat)
        {
            return true;
        }

        // A free-format frame's length is only the distance to the next sync, which junk can fake as
        // easily as the sync itself: a third frame at the same distance confirms it, padding aside.
        var stream = header;
        var third = next + length;
        return following.IsFreeFormat && (FreeAt(third) || FreeAt(third + 1) || FreeAt(third - 1));

        bool FreeAt(long position) =>
            TryHeaderAt(reader, position, end, out var candidate) && candidate.Matches(stream) && candidate.IsFreeFormat;
    }

    private static bool TryHeaderAt(BoundedReader reader, long at, long end, out MpegFrameHeader header)
    {
        header = default;
        return at + MpegFrameHeader.Length <= end
            && reader.TryRead(ReadPurpose.Search, at, MpegFrameHeader.Length, out var bytes)
            && MpegFrameHeader.TryParse(bytes.Span, out header);
    }

    /// <summary>A free-format frame ends where the next header with the same fixed bits starts; 0 if none is near.</summary>
    private static int FreeFormatLength(BoundedReader reader, long at, long end)
    {
        var to = Math.Min(end, at + MpegFrameHeader.Length + FreeFormatReach);
        if (!reader.TryRead(ReadPurpose.Search, at, to - at, out var block))
        {
            return 0;
        }

        var span = block.Span;
        for (var i = MpegFrameHeader.Length; i + MpegFrameHeader.Length <= span.Length; i++)
        {
            // The padding bit may differ from frame to frame, so it is left out of the comparison.
            if (span[i] == span[0] && span[i + 1] == span[1] && (span[i + 2] & 0xFC) == (span[2] & 0xFC)
                && MpegFrameHeader.TryParse(span[i..], out _))
            {
                return i;
            }
        }

        return 0;
    }
}
