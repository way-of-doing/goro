namespace Goro.Reading.Flac;

/// <summary>
/// How a FLAC frame codes its channels (RFC 9639, section 9.1.3): independently, or as a stereo
/// pair of which one channel is a difference, and so has a bit more depth than the stream.
/// </summary>
public enum FlacChannelAssignment
{
    Independent,
    LeftSide,
    SideRight,
    MidSide,
}

/// <summary>
/// A FLAC frame header that passed its CRC-8 (RFC 9639, section 9.1). A frame records no length, so
/// the header is all that says a frame starts here; the frame ends where the next one starts, or
/// where its body says (<see cref="FlacFrameBody"/>).
/// </summary>
/// <param name="VariableBlocking">Whether <paramref name="Number"/> is a sample number rather than a frame number.</param>
/// <param name="Number">The frame number in a stream of fixed block size, the first sample's number in one of variable block size.</param>
/// <param name="SampleRate">The sample rate in Hz, or 0 where the header defers to STREAMINFO.</param>
/// <param name="BitDepth">Bits per sample, or 0 where the header defers to STREAMINFO.</param>
/// <param name="Length">The header's length in bytes, its CRC-8 included.</param>
public readonly record struct FlacFrameHeader(
    bool VariableBlocking, ulong Number, int BlockSize, int SampleRate, int Channels, FlacChannelAssignment Assignment, int BitDepth, int Length)
{
    /// <summary>The longest a header can be: sync and codes, a 7-byte number, an uncommon block size and sample rate, the CRC.</summary>
    public const int MaxLength = 16;

    /// <summary>The number of the frame's first sample, given the block size every frame but the last has in a stream of fixed block size.</summary>
    public ulong FirstSample(int nominalBlockSize) => VariableBlocking ? Number : Number * (ulong)nominalBlockSize;

    /// <summary>The number of the sample after the frame's last: where the timeline ends if this frame is the last.</summary>
    public ulong EndSample(int nominalBlockSize) => FirstSample(nominalBlockSize) + (ulong)BlockSize;

    /// <summary>Parses a header at the start of <paramref name="bytes"/>, which may hold more after it.</summary>
    public static bool TryParse(ReadOnlySpan<byte> bytes, out FlacFrameHeader header) =>
        TryParseIgnoringCrc(bytes, out header, out var crcPasses) && crcPasses;

    /// <summary>
    /// Parses a header whether or not its CRC-8 passes, saying which: what a damaged header looks
    /// like, every field well formed and the checksum not matching them.
    /// </summary>
    public static bool TryParseIgnoringCrc(ReadOnlySpan<byte> bytes, out FlacFrameHeader header, out bool crcPasses)
    {
        header = default;
        crcPasses = false;
        if (bytes.Length < 6 || bytes[0] != 0xFF || (bytes[1] & 0xFE) != 0xF8)
        {
            return false;
        }

        var variable = (bytes[1] & 1) == 1;
        var blockCode = bytes[2] >> 4;
        var rateCode = bytes[2] & 0x0F;
        var channelCode = bytes[3] >> 4;
        var depthCode = (bytes[3] >> 1) & 7;
        if (blockCode == 0 || rateCode == 15 || channelCode > 10 || depthCode == 3 || (bytes[3] & 1) != 0)
        {
            return false;
        }

        var at = 4;
        if (!TryCodedNumber(bytes, ref at, out var number) || (!variable && number > int.MaxValue))
        {
            return false;
        }

        var blockSize = blockCode switch
        {
            1 => 192,
            >= 2 and <= 5 => 576 << (blockCode - 2),
            >= 8 => 256 << (blockCode - 8),
            _ => 0,
        };
        if (blockCode == 6)
        {
            if (at + 1 > bytes.Length)
            {
                return false;
            }

            blockSize = bytes[at++] + 1;
        }
        else if (blockCode == 7)
        {
            if (at + 2 > bytes.Length || (bytes[at] == 0xFF && bytes[at + 1] == 0xFF))
            {
                return false;
            }

            blockSize = Binary.U16BigEndian(bytes[at..]) + 1;
            at += 2;
        }

        var sampleRate = rateCode switch
        {
            0 => 0, 1 => 88200, 2 => 176400, 3 => 192000, 4 => 8000, 5 => 16000, 6 => 22050, 7 => 24000,
            8 => 32000, 9 => 44100, 10 => 48000, 11 => 96000, _ => -1,
        };
        if (rateCode == 12)
        {
            if (at + 1 > bytes.Length)
            {
                return false;
            }

            sampleRate = bytes[at++] * 1000;
        }
        else if (rateCode is 13 or 14)
        {
            if (at + 2 > bytes.Length)
            {
                return false;
            }

            sampleRate = Binary.U16BigEndian(bytes[at..]) * (rateCode == 14 ? 10 : 1);
            at += 2;
        }

        if (at + 1 > bytes.Length)
        {
            return false;
        }

        crcPasses = Checksums.Crc8(bytes[..at]) == bytes[at];

        var (channels, assignment) = channelCode switch
        {
            8 => (2, FlacChannelAssignment.LeftSide),
            9 => (2, FlacChannelAssignment.SideRight),
            10 => (2, FlacChannelAssignment.MidSide),
            _ => (channelCode + 1, FlacChannelAssignment.Independent),
        };
        var depth = depthCode switch { 0 => 0, 1 => 8, 2 => 12, 4 => 16, 5 => 20, 6 => 24, _ => 32 };
        header = new FlacFrameHeader(variable, number, blockSize, sampleRate, channels, assignment, depth, at + 1);
        return true;
    }

    /// <summary>
    /// The frame or sample number, coded like UTF-8 extended to seven bytes and 36 bits (RFC 9639,
    /// section 9.1.5).
    /// </summary>
    private static bool TryCodedNumber(ReadOnlySpan<byte> bytes, ref int at, out ulong number)
    {
        number = 0;
        if (at >= bytes.Length)
        {
            return false;
        }

        int lead = bytes[at];
        int extra;
        if (lead < 0x80)
        {
            extra = 0;
            number = (ulong)lead;
        }
        else if ((lead & 0xE0) == 0xC0)
        {
            extra = 1;
            number = (ulong)(lead & 0x1F);
        }
        else if ((lead & 0xF0) == 0xE0)
        {
            extra = 2;
            number = (ulong)(lead & 0x0F);
        }
        else if ((lead & 0xF8) == 0xF0)
        {
            extra = 3;
            number = (ulong)(lead & 0x07);
        }
        else if ((lead & 0xFC) == 0xF8)
        {
            extra = 4;
            number = (ulong)(lead & 0x03);
        }
        else if ((lead & 0xFE) == 0xFC)
        {
            extra = 5;
            number = (ulong)(lead & 0x01);
        }
        else if (lead == 0xFE)
        {
            extra = 6;
        }
        else
        {
            return false;
        }

        if (at + 1 + extra > bytes.Length)
        {
            return false;
        }

        for (var k = 1; k <= extra; k++)
        {
            var next = bytes[at + k];
            if ((next & 0xC0) != 0x80)
            {
                return false;
            }

            number = (number << 6) | (ulong)(next & 0x3Fu);
        }

        at += 1 + extra;
        return true;
    }
}
