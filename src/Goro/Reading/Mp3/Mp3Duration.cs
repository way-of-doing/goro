using Goro.Reading.Bytes;

namespace Goro.Reading.Mp3;

/// <summary>Which summary an MP3's first frame holds.</summary>
public enum SummaryKind
{
    /// <summary>A <c>Xing</c> header, written for a variable bitrate.</summary>
    Xing,

    /// <summary>An <c>Info</c> header: the same layout, written for a constant bitrate.</summary>
    Info,

    /// <summary>A <c>VBRI</c> header, Fraunhofer's.</summary>
    Vbri,
}

/// <summary>The LAME tag behind a Xing or Info header: the encoder's delay and padding, in samples, and whether its CRC passes.</summary>
public sealed record LameTag(int Delay, int Padding, bool CrcPasses);

/// <summary>
/// The summary frame an encoder writes ahead of the audio: what it records, where it records it.
/// <paramref name="AtUnprotectedOffset"/> says the header sits where it would in a frame without a
/// CRC although the frame has one, which is where LAME writes it.
/// </summary>
public sealed record SummaryFrame(SummaryKind Kind, long? FrameCount, long? ByteCount, LameTag? Lame, bool AtUnprotectedOffset);

/// <summary>
/// Everything that decides an MP3's playing time, in one place (docs/implementation.md): the
/// summary header and the checks that let it be trusted, and, for a file without one, the count of
/// a constant-bitrate stream from its edges. Nothing here walks the frames.
/// </summary>
public static class Mp3Duration
{
    /// <summary>How many evenly spaced points a constant bitrate is confirmed at.</summary>
    private const int Probes = 7;

    /// <summary>How far each probe searches for a frame.</summary>
    private const int ProbeReach = 16 * 1024;

    /// <summary>
    /// The playing time of the audio from its first frame, <paramref name="firstFrame"/> (<c>C</c> in
    /// docs/design/mp3-layout.md), to where the trailing tags start, <paramref name="trailingTagsStart"/>
    /// (<c>F</c>), and the summary frame the decision rests on, if there is one. Junk after the audio
    /// a trusted header describes is added to <paramref name="conditions"/>.
    /// </summary>
    public static (DurationOutcome Duration, SummaryFrame? Summary) Decide(
        BoundedReader reader, long firstFrame, long trailingTagsStart, ICollection<Condition> conditions)
    {
        if (!MpegFrames.IsConfirmed(reader, firstFrame, trailingTagsStart, out var first))
        {
            return (new DurationOutcome.Unusable(DurationProblem.NoAudio), null);
        }

        var summary = ReadSummary(reader, firstFrame, trailingTagsStart, first);
        if (summary?.FrameCount is not { } frames)
        {
            return (ConstantBitrate(reader, firstFrame, trailingTagsStart, first), summary);
        }

        return (FromSummary(reader, firstFrame, trailingTagsStart, first, summary, frames, conditions), summary);
    }

    private static DurationOutcome FromSummary(
        BoundedReader reader, long firstFrame, long end, MpegFrameHeader first, SummaryFrame summary, long frames, ICollection<Condition> conditions)
    {
        if (summary.Lame is { CrcPasses: false })
        {
            return new DurationOutcome.Unusable(DurationProblem.SummaryCrcFails);
        }

        if (summary.ByteCount is not { } claimed)
        {
            return new DurationOutcome.Unusable(DurationProblem.NoByteCount);
        }

        var samples = frames * first.SamplesPerFrame - (summary.Lame is { } lame ? lame.Delay + lame.Padding : 0);
        if (samples < 0)
        {
            return new DurationOutcome.Unusable(DurationProblem.GaplessExceedsStream);
        }

        // Within one frame of the audio found: the header describes the file.
        var audio = end - firstFrame;
        if (Math.Abs(claimed - audio) <= MpegFrameHeader.MaxFrameLength)
        {
            return new DurationOutcome.Known(samples, first.SampleRate, DurationBasis.SummaryHeader);
        }

        // More than the file holds: it was cut short.
        if (claimed > audio)
        {
            return new DurationOutcome.Unusable(DurationProblem.SummaryDisagrees);
        }

        // Less than the file holds: junk after the audio, if a frame ends where the header says the
        // audio does and none starts there; more audio, from a file joined on, otherwise.
        var audioEnd = firstFrame + claimed;
        if (MpegFrames.FrameEndsAt(reader, audioEnd, firstFrame, first, sameBitrate: false) && !MpegFrames.FrameStartsAt(reader, audioEnd, end, first))
        {
            conditions.Add(new BytesAfterAudio(Region.Between(audioEnd, end)));
            return new DurationOutcome.Known(samples, first.SampleRate, DurationBasis.SummaryHeaderToFrameEnd);
        }

        return new DurationOutcome.Unusable(DurationProblem.SummaryDisagrees);
    }

    /// <summary>
    /// The frame count of a constant-bitrate stream without a summary header, if it can be shown to be
    /// one from its edges: the first frame's bitrate is found again at seven evenly spaced points, and
    /// a frame of that bitrate ends exactly where the trailing tags start.
    /// </summary>
    private static DurationOutcome ConstantBitrate(BoundedReader reader, long firstFrame, long end, MpegFrameHeader first)
    {
        if (first.IsFreeFormat)
        {
            return new DurationOutcome.Unusable(DurationProblem.FreeFormatWithoutHeader);
        }

        var length = end - firstFrame;
        for (var k = 1; k <= Probes; k++)
        {
            var at = firstFrame + length * k / (Probes + 1);
            if (MpegFrames.FindOfStream(reader, at, Math.Min(end, at + ProbeReach), end, first) is not { } found
                || found.Header.BitrateKbps != first.BitrateKbps)
            {
                return new DurationOutcome.Unusable(DurationProblem.NotConstantBitrate);
            }
        }

        if (!MpegFrames.FrameEndsAt(reader, end, firstFrame, first, sameBitrate: true))
        {
            return new DurationOutcome.Unusable(DurationProblem.NotConstantBitrate);
        }

        var frames = (long)Math.Round(length / first.AverageFrameLength);
        return new DurationOutcome.Known(frames * first.SamplesPerFrame, first.SampleRate, DurationBasis.ConstantBitrateFromEdges);
    }

    /// <summary>The Xing, Info or VBRI header in the first frame, with the LAME tag behind it; null if there is none.</summary>
    private static SummaryFrame? ReadSummary(BoundedReader reader, long firstFrame, long end, MpegFrameHeader first)
    {
        if (!reader.TryRead(ReadPurpose.DeclaredStructure, firstFrame, Math.Min(first.FrameLength, end - firstFrame), out var bytes))
        {
            return null;
        }

        var frame = bytes.Span;

        // LAME writes its header where a frame without a CRC would have it, even in a frame with one.
        var at = first.SummaryOffset;
        var unprotected = false;
        if (first.Crc && !IsXingOrInfo(frame, at) && IsXingOrInfo(frame, at - 2))
        {
            at -= 2;
            unprotected = true;
        }

        if (IsXingOrInfo(frame, at))
        {
            return Xing(frame, at, unprotected);
        }

        // VBRI sits 32 bytes after the frame header, its byte count and frame count at 10 and 14.
        const int vbri = MpegFrameHeader.Length + 32;
        return frame.Length >= vbri + 18 && frame[vbri..].StartsWith("VBRI"u8)
            ? new SummaryFrame(SummaryKind.Vbri, Binary.U32BigEndian(frame[(vbri + 14)..]), Binary.U32BigEndian(frame[(vbri + 10)..]), null, false)
            : null;
    }

    private static bool IsXingOrInfo(ReadOnlySpan<byte> frame, int at) =>
        at >= 0 && at + 4 <= frame.Length && (frame[at..].StartsWith("Xing"u8) || frame[at..].StartsWith("Info"u8));

    /// <summary>A Xing or Info header: flags, then the frame count, byte count, seek table and quality each flag announces.</summary>
    private static SummaryFrame Xing(ReadOnlySpan<byte> frame, int at, bool unprotected)
    {
        var kind = frame[at..].StartsWith("Xing"u8) ? SummaryKind.Xing : SummaryKind.Info;
        var flags = at + 8 <= frame.Length ? Binary.U32BigEndian(frame[(at + 4)..]) : 0;
        var o = at + 8;
        long? frames = null, bytes = null;
        if ((flags & 1) != 0 && o + 4 <= frame.Length)
        {
            frames = Binary.U32BigEndian(frame[o..]);
            o += 4;
        }

        if ((flags & 2) != 0 && o + 4 <= frame.Length)
        {
            bytes = Binary.U32BigEndian(frame[o..]);
            o += 4;
        }

        o += (flags & 4) != 0 ? 100 : 0;
        o += (flags & 8) != 0 ? 4 : 0;
        return new SummaryFrame(kind, frames, bytes, Lame(frame, o), unprotected);
    }

    /// <summary>
    /// The LAME tag at <paramref name="at"/>, written by LAME and by ffmpeg's encoders: delay and padding,
    /// twelve bits each, at bytes 21 to 23, and a CRC-16/ARC at 34 over every byte of the frame before it.
    /// </summary>
    private static LameTag? Lame(ReadOnlySpan<byte> frame, int at)
    {
        if (at + 24 > frame.Length || !(frame[at..].StartsWith("LAME"u8) || frame[at..].StartsWith("Lavc"u8) || frame[at..].StartsWith("Lavf"u8)))
        {
            return null;
        }

        var delay = frame[at + 21] << 4 | frame[at + 22] >> 4;
        var padding = (frame[at + 22] & 0x0F) << 8 | frame[at + 23];
        var crcPasses = at + 36 > frame.Length || Binary.Crc16Arc(frame[..(at + 34)]) == Binary.U16BigEndian(frame[(at + 34)..]);
        return new LameTag(delay, padding, crcPasses);
    }
}
