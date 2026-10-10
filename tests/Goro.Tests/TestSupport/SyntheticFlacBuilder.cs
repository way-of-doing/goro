namespace Goro.Tests.TestSupport;

/// <summary>
/// Builds FLAC frames bit by bit for tests, with real CRCs, so that the reader's frame parsing can
/// be checked on shapes the encoder-made seeds never hold: every channel assignment, wasted bits,
/// escaped partitions, forbidden values. The CRCs are computed here rather than by the reader, so a
/// mistake in one cannot hide in the other.
/// </summary>
internal static class SyntheticFlacBuilder
{
    /// <summary>A subframe, written into a frame by <see cref="Frame"/>.</summary>
    public delegate void Subframe(BitWriter bits, int blockSize, int depth);

    /// <summary>
    /// A frame of a stream of fixed block size at 44.1 kHz, its block size stored as an uncommon
    /// 16-bit size so any size can be had.
    /// </summary>
    /// <param name="channelCode">The channels bits: 0–7 for 1–8 independent channels, 8–10 for left/side, side/right, mid/side.</param>
    /// <param name="depthCode">The bit depth bits: 4 for 16 bits, 0 to defer to STREAMINFO.</param>
    public static byte[] Frame(ulong number, int blockSize, int channelCode, int depthCode, bool variable, params Subframe[] subframes) =>
        Frame(number, blockSize, channelCode, depthCode, variable, rateCode: 9, subframes);

    /// <param name="rateCode">The sample rate bits: 9 for 44.1 kHz, 0 to defer to STREAMINFO.</param>
    public static byte[] Frame(ulong number, int blockSize, int channelCode, int depthCode, bool variable, int rateCode, Subframe[] subframes)
    {
        var header = new List<byte> { 0xFF, (byte)(variable ? 0xF9 : 0xF8), (byte)((7 << 4) | rateCode), (byte)((channelCode << 4) | (depthCode << 1)) };
        header.AddRange(CodedNumber(number));
        header.Add((byte)((blockSize - 1) >> 8));
        header.Add((byte)(blockSize - 1));
        header.Add(Crc8([.. header]));

        var depth = depthCode switch { 1 => 8, 2 => 12, 4 => 16, 5 => 20, 6 => 24, 7 => 32, _ => 16 };
        var bits = new BitWriter();
        for (var channel = 0; channel < subframes.Length; channel++)
        {
            var side = (channelCode == 8 && channel == 1) || (channelCode == 9 && channel == 0) || (channelCode == 10 && channel == 1);
            subframes[channel](bits, blockSize, depth + (side ? 1 : 0));
        }

        var body = header.Concat(bits.ToBytes()).ToArray();
        var crc = Crc16(body);
        return [.. body, (byte)(crc >> 8), (byte)crc];
    }

    /// <summary>
    /// A whole stream: <c>fLaC</c>, a STREAMINFO block flagged last, then <paramref name="frames"/>.
    /// The frames are mono, 16 bits, 44.1 kHz, each a constant subframe, so a stream of any length is
    /// cheap; a frame number past <paramref name="blockSizes"/>' end is never made.
    /// </summary>
    /// <param name="blockSizes">Each frame's block size, in order.</param>
    /// <param name="variable">Whether frames are numbered by sample rather than by frame.</param>
    public static byte[] Stream(ulong totalSamples, int[] blockSizes, bool variable = false, int streamRate = 44100, int frameRateCode = 9)
    {
        var maxBlock = blockSizes.Max();
        var bytes = new List<byte>("fLaC"u8.ToArray()) { 0x80, 0x00, 0x00, 0x22 };
        bytes.AddRange(StreamInfo(variable ? blockSizes.Min() : maxBlock, maxBlock, streamRate, totalSamples));
        ulong sample = 0;
        for (var n = 0; n < blockSizes.Length; n++)
        {
            bytes.AddRange(Frame(variable ? sample : (ulong)n, blockSizes[n], 0, 4, variable, frameRateCode, [Constant()]));
            sample += (ulong)blockSizes[n];
        }

        return [.. bytes];
    }

    /// <summary>
    /// A stream of two frames whose metadata is a STREAMINFO and a VORBIS_COMMENT block holding
    /// <paramref name="comments"/>, each written as given, so that a comment can hold any bytes.
    /// </summary>
    public static byte[] WithComments(int blockSize, byte[][] comments)
    {
        var list = new List<byte>();
        list.AddRange(BitConverter.GetBytes(4u));
        list.AddRange("test"u8.ToArray());
        list.AddRange(BitConverter.GetBytes((uint)comments.Length));
        foreach (var comment in comments)
        {
            list.AddRange(BitConverter.GetBytes((uint)comment.Length));
            list.AddRange(comment);
        }

        var bytes = new List<byte>("fLaC"u8.ToArray()) { 0x00, 0x00, 0x00, 0x22 };
        bytes.AddRange(StreamInfo(blockSize, blockSize, 44100, 2UL * (ulong)blockSize));
        bytes.AddRange([0x84, (byte)(list.Count >> 16), (byte)(list.Count >> 8), (byte)list.Count]);
        bytes.AddRange(list);
        bytes.AddRange(Frame(0, blockSize, 0, 4, false, Constant()));
        bytes.AddRange(Frame(1, blockSize, 0, 4, false, Constant()));
        return [.. bytes];
    }

    /// <summary>A STREAMINFO payload: mono, 16 bits, frame sizes unknown, no MD5.</summary>
    public static byte[] StreamInfo(int minBlock, int maxBlock, int rate, ulong totalSamples)
    {
        var info = new byte[34];
        info[0] = (byte)(minBlock >> 8);
        info[1] = (byte)minBlock;
        info[2] = (byte)(maxBlock >> 8);
        info[3] = (byte)maxBlock;
        var packed = ((ulong)rate << 44) | (0UL << 41) | (15UL << 36) | totalSamples;
        for (var i = 0; i < 8; i++)
        {
            info[10 + i] = (byte)(packed >> (56 - 8 * i));
        }

        return info;
    }

    public static Subframe Constant(long value = 0) => (bits, _, depth) =>
    {
        bits.Write(0, 8);
        bits.Write((ulong)value, depth);
    };

    public static Subframe Verbatim() => (bits, blockSize, depth) =>
    {
        bits.Write(1 << 1, 8);
        for (var i = 0; i < blockSize; i++)
        {
            bits.Write((ulong)(i & 1), depth);
        }
    };

    /// <summary>A verbatim subframe whose samples have <paramref name="wasted"/> wasted bits, so are that much narrower.</summary>
    public static Subframe VerbatimWithWastedBits(int wasted) => (bits, blockSize, depth) =>
    {
        bits.Write((1 << 1) | 1, 8);
        bits.Write(1, wasted);
        for (var i = 0; i < blockSize; i++)
        {
            bits.Write(0, depth - wasted);
        }
    };

    /// <summary>A fixed-predictor subframe whose residual is one escaped partition of <paramref name="width"/>-bit samples.</summary>
    public static Subframe FixedEscaped(int order, int width) => (bits, blockSize, depth) =>
    {
        bits.Write((ulong)((8 + order) << 1), 8);
        bits.Write(0, order * depth);
        bits.Write(0, 2);
        bits.Write(0, 4);
        bits.Write(15, 4);
        bits.Write((ulong)width, 5);
        bits.Write(0, (blockSize - order) * width);
    };

    /// <summary>A fixed-predictor subframe whose residual is Rice-coded: <paramref name="partitionOrder"/> partitions, every sample 0 under parameter 2.</summary>
    public static Subframe FixedRice(int order, int partitionOrder) => (bits, blockSize, depth) =>
    {
        bits.Write((ulong)((8 + order) << 1), 8);
        bits.Write(0, order * depth);
        bits.Write(0, 2);
        bits.Write((ulong)partitionOrder, 4);
        for (var p = 0; p < 1 << partitionOrder; p++)
        {
            bits.Write(2, 4);
            var samples = (blockSize >> partitionOrder) - (p == 0 ? order : 0);
            for (var s = 0; s < samples; s++)
            {
                bits.Write(1, 1);
                bits.Write(0, 2);
            }
        }
    };

    /// <summary>A linear-prediction subframe whose coefficient precision bits are 0b1111, which RFC 9639 forbids.</summary>
    public static Subframe LpcWithForbiddenPrecision() => (bits, _, depth) =>
    {
        bits.Write((ulong)((32 + 0) << 1), 8);
        bits.Write(0, depth);
        bits.Write(0b1111, 4);
        bits.Write(0, 5);
    };

    /// <summary>A subframe of a reserved type.</summary>
    public static Subframe Reserved() => (bits, _, _) => bits.Write(2 << 1, 8);

    /// <summary>The frame or sample number, coded like UTF-8 extended to seven bytes.</summary>
    public static byte[] CodedNumber(ulong value)
    {
        if (value < 0x80)
        {
            return [(byte)value];
        }

        var extra = value < 0x800 ? 1 : value < 0x10000 ? 2 : value < 0x200000 ? 3 : value < 0x4000000 ? 4 : value < 0x80000000 ? 5 : 6;
        var bytes = new byte[extra + 1];
        for (var i = extra; i > 0; i--)
        {
            bytes[i] = (byte)(0x80 | (value & 0x3F));
            value >>= 6;
        }

        bytes[0] = extra == 6 ? (byte)0xFE : (byte)((0xFF00 >> (extra + 1)) & 0xFF | (int)value);
        return bytes;
    }

    public static byte Crc8(byte[] data)
    {
        var crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x80) != 0 ? ((crc << 1) ^ 0x07) & 0xFF : (crc << 1) & 0xFF;
            }
        }

        return (byte)crc;
    }

    public static ushort Crc16(byte[] data)
    {
        var crc = 0;
        foreach (var b in data)
        {
            crc ^= b << 8;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x8005) & 0xFFFF : (crc << 1) & 0xFFFF;
            }
        }

        return (ushort)crc;
    }

    /// <summary>Writes bits from the high bit of each byte down, padding the last byte with zeros.</summary>
    public sealed class BitWriter
    {
        private readonly List<byte> bytes = [];
        private int used = 8;

        public void Write(ulong value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                if (used == 8)
                {
                    bytes.Add(0);
                    used = 0;
                }

                if (((value >> i) & 1) == 1)
                {
                    bytes[^1] |= (byte)(0x80 >> used);
                }

                used++;
            }
        }

        public byte[] ToBytes() => [.. bytes];
    }
}
