using static Goro.Scratchpad.FileReading.Bin;

namespace Goro.Scratchpad.FileReading;

/// <summary>
/// Everything that decides an MP3's playing time, in one place, as implementation.md asks: the
/// summary header (Xing, Info or VBRI) and the checks that let it be trusted, and, for a file
/// without one, the constant-bitrate method that counts frames from the edges. Nothing here walks
/// the frames; the scan in <see cref="FormatReaders"/> is evidence for audit, never the answer.
/// </summary>
public static class Mp3Duration
{
    /// <summary>The longest frame there is (MPEG-1 Layer II at 384 kbps and 32 kHz, padded), and so how far back a frame end is looked for.</summary>
    const int MaxFrameLength = 2881;

    /// <summary>How far each constant-bitrate probe searches for a frame.</summary>
    const int ProbeWindow = 16 * 1024;

    const int Probes = 7;

    public sealed record Outcome(
        DurationEvidence Policy,
        List<DurationEvidence> Evidence,
        List<string> Problems,
        bool FirstIsInfo,
        bool HasLame,
        int Delay,
        int Padding);

    /// <summary>
    /// Decides the playing time of the audio from the confirmed frame at <paramref name="first"/>
    /// (position C) to <paramref name="end"/> (position F, where the trailing tags start).
    /// </summary>
    public static Outcome Decide(byte[] f, long first, long end, MpegHeader h)
    {
        var evidence = new List<DurationEvidence>();
        var problems = new List<string>();
        var spf = h.SamplesPerFrame;
        var header = SummaryHeader(f, first, h, problems);

        if (header.Frames is { } frames)
        {
            evidence.Add(new DurationEvidence(header.Kind, frames * spf, h.SampleRate));
            if (header.HasLame)
            {
                evidence.Add(new DurationEvidence($"{header.Kind}-gapless", frames * spf - header.Delay - header.Padding, h.SampleRate,
                    $"delay {header.Delay}, padding {header.Padding}"));
            }
        }

        DurationEvidence policy;
        if (header.Frames is null)
        {
            policy = ConstantBitrate(f, first, end, h, problems) is { } frameCount
                ? new DurationEvidence("policy", frameCount * spf, h.SampleRate, "no summary header: constant bitrate, counted from the edges")
                : new DurationEvidence("policy", 0, 0, "no summary header, and not shown to be constant bitrate: unusable");
        }
        else
        {
            policy = Trusted(f, first, end, h, header, problems)
                ? new DurationEvidence("policy", header.Frames.Value * spf - (header.HasLame ? header.Delay + header.Padding : 0), h.SampleRate,
                    "summary header, trusted")
                : new DurationEvidence("policy", 0, 0, !header.CrcOk
                    ? "summary header fails its CRC: unusable"
                    : $"summary header disagrees (bytes {header.Bytes} vs {end - first}): unusable");
        }

        return new Outcome(policy, evidence, problems, header.Present, header.HasLame, header.Delay, header.Padding);
    }

    sealed record Header(bool Present, string Kind, long? Frames, long? Bytes, bool HasLame, int Delay, int Padding, bool CrcOk);

    /// <summary>The Xing, Info or VBRI header in the first frame, if any, with the LAME tag behind it.</summary>
    static Header SummaryHeader(byte[] f, long first, MpegHeader h, List<string> problems)
    {
        // LAME places the tag as if there were no CRC, even in a CRC-protected frame, so look at both places.
        var at = (int)first + h.XingOffset;
        bool IsXing(int i) => StartsWith(f, i, "Xing") || StartsWith(f, i, "Info");
        if (h.Crc && !IsXing(at) && IsXing(at - 2))
        {
            at -= 2;
            problems.Add("Info tag written at the offset of a frame without CRC");
        }

        bool Fits(int i, int length) => i + length <= f.Length;
        if (IsXing(at))
        {
            var flags = Fits(at + 4, 4) ? ReadU32BE(f, at + 4) : 0;
            var o = at + 8;
            long? frames = null, bytes = null;
            if ((flags & 1) != 0 && Fits(o, 4))
            {
                frames = ReadU32BE(f, o);
                o += 4;
            }

            if ((flags & 2) != 0 && Fits(o, 4))
            {
                bytes = ReadU32BE(f, o);
                o += 4;
            }

            o += (flags & 4) != 0 ? 100 : 0;
            o += (flags & 8) != 0 ? 4 : 0;

            // The LAME tag: a 9-byte encoder string, delay and padding at bytes 21-23, and a CRC-16/ARC
            // at 34-35 over every byte of the frame before it, Xing counts included.
            if (Fits(o, 24) && (StartsWith(f, o, "LAME") || StartsWith(f, o, "Lavc") || StartsWith(f, o, "Lavf")))
            {
                var delay = f[o + 21] << 4 | f[o + 22] >> 4;
                var padding = (f[o + 22] & 0x0F) << 8 | f[o + 23];
                var crcOk = !Fits(o, 36) || Crc16Arc(f.AsSpan((int)first, o + 34 - (int)first)) == ReadU16BE(f, o + 34);
                if (!crcOk)
                {
                    problems.Add("LAME tag CRC fails: the Info frame is damaged");
                }

                return new Header(true, "xing", frames, bytes, true, delay, padding, crcOk);
            }

            return new Header(true, "xing", frames, bytes, false, 0, 0, true);
        }

        if (StartsWith(f, (int)first + 36, "VBRI") && Fits((int)first + 36, 18))
        {
            return new Header(true, "vbri", ReadU32BE(f, (int)first + 36 + 14), ReadU32BE(f, (int)first + 36 + 10), false, 0, 0, true);
        }

        return new Header(false, "none", null, null, false, 0, 0, true);
    }

    /// <summary>
    /// Whether a summary header can be trusted: its CRC passes, and its byte count agrees with the
    /// audio found, within one frame, or ends exactly at a frame end where no frame starts (the rest
    /// being junk; see implementation.md).
    /// </summary>
    static bool Trusted(byte[] f, long first, long end, MpegHeader h, Header header, List<string> problems)
    {
        if (!header.CrcOk || header.Bytes is not { } claimed)
        {
            return false;
        }

        var audioBytes = end - first;
        if (Math.Abs(claimed - audioBytes) <= MaxFrameLength)
        {
            return true;
        }

        if (claimed > audioBytes)
        {
            return false; // the file holds less than the header describes: truncated
        }

        var audioEnd = first + claimed;
        var frameStartsThere = MpegHeader.TryParse(f, (int)audioEnd, out var after) && after.Matches(h);
        if (FrameEndsAt(f, audioEnd, first, h, sameBitrate: false) && !frameStartsThere)
        {
            problems.Add($"{end - audioEnd} bytes after the audio the summary header describes");
            return true;
        }

        return false;
    }

    /// <summary>
    /// The frame count of a constant-bitrate file without a summary header, or null if it cannot be
    /// shown to be one: the first frame's bitrate is found again at seven evenly spaced points, and a
    /// frame of that bitrate ends exactly where the trailing tags start.
    /// </summary>
    static long? ConstantBitrate(byte[] f, long first, long end, MpegHeader h, List<string> problems)
    {
        if (h.BitrateKbps == 0)
        {
            problems.Add("free format without a summary header");
            return null;
        }

        var length = end - first;
        for (var k = 1; k <= Probes; k++)
        {
            var at = first + length * k / (Probes + 1);
            var found = FormatReaders.FindConfirmedFrame(f, at, Math.Min(end, at + ProbeWindow), h);
            if (found is not { } frame || frame.Header.BitrateKbps != h.BitrateKbps)
            {
                problems.Add(found is null
                    ? $"constant-bitrate check: no frame near byte {at}"
                    : $"constant-bitrate check: {found.Value.Header.BitrateKbps} kbps at byte {found.Value.At}, not {h.BitrateKbps}");
                return null;
            }
        }

        if (!FrameEndsAt(f, end, first, h, sameBitrate: true))
        {
            problems.Add("constant-bitrate check: no frame ends where the audio does");
            return null;
        }

        return (long)Math.Round(length / h.AverageFrameLength);
    }

    /// <summary>Whether a frame matching the stream, and of the same bitrate if asked, ends exactly at <paramref name="at"/>.</summary>
    static bool FrameEndsAt(byte[] f, long at, long first, MpegHeader h, bool sameBitrate)
    {
        for (var i = at - 4; i >= Math.Max(first, at - MaxFrameLength); i--)
        {
            if (MpegHeader.TryParse(f, (int)i, out var last) && last.Matches(h) && last.FrameLength > 0
                && i + last.FrameLength == at && (!sameBitrate || last.BitrateKbps == h.BitrateKbps))
            {
                return true;
            }
        }

        return false;
    }
}
