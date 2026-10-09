namespace Goro.Reading.Flac;

/// <summary>Where a FLAC frame's body says the frame ends.</summary>
public abstract record FlacFrameEnd
{
    private FlacFrameEnd()
    {
    }

    /// <param name="Length">The frame's length in bytes, its header and CRC-16 included.</param>
    /// <param name="CrcPasses">Whether the CRC-16 at its end matches what precedes it.</param>
    public sealed record EndsAt(int Length, bool CrcPasses) : FlacFrameEnd;

    /// <summary>The bytes ran out before the frame did: a frame cut off, as truncation leaves one.</summary>
    public sealed record RunsPast : FlacFrameEnd;

    /// <summary>The body holds something the format forbids or reserves, so where it ends cannot be known.</summary>
    public sealed record Malformed : FlacFrameEnd;
}

/// <summary>
/// Finds where a FLAC frame ends by walking its body: every subframe's header, its warm-up samples
/// and coefficients, and its Rice-coded residual, down to the bit, then the padding to a byte and
/// the CRC-16 (RFC 9639, section 9.2). Nothing is decoded into samples. It is what gives the end of
/// a frame with no frame after it, since a frame records no length, and a CRC-16 that happens to
/// match is no proof of an end (docs/implementation.md, "Finding where a FLAC file's audio ends").
/// </summary>
public static class FlacFrameBody
{
    /// <summary>
    /// Where the frame at the start of <paramref name="frame"/>, whose header is
    /// <paramref name="header"/>, ends. The bytes may run on past it, or stop short of it.
    /// </summary>
    /// <param name="streamBitDepth">STREAMINFO's bits per sample, for a header that defers to it; 0 where there is none.</param>
    public static FlacFrameEnd FindEnd(ReadOnlySpan<byte> frame, FlacFrameHeader header, int streamBitDepth)
    {
        var depth = header.BitDepth != 0 ? header.BitDepth : streamBitDepth;
        if (depth is < 4 or > 32 || header.Length > frame.Length)
        {
            return depth is < 4 or > 32 ? new FlacFrameEnd.Malformed() : new FlacFrameEnd.RunsPast();
        }

        var bits = new BitCursor(frame, header.Length * 8L);
        for (var channel = 0; channel < header.Channels; channel++)
        {
            var outcome = SkipSubframe(ref bits, header.BlockSize, depth + (IsSideChannel(header.Assignment, channel) ? 1 : 0));
            if (outcome != Step.Done)
            {
                return outcome == Step.RanOut ? new FlacFrameEnd.RunsPast() : new FlacFrameEnd.Malformed();
            }
        }

        var bodyEnd = (int)((bits.Position + 7) / 8);
        var length = bodyEnd + 2;
        if (length > frame.Length)
        {
            return new FlacFrameEnd.RunsPast();
        }

        return new FlacFrameEnd.EndsAt(length, Checksums.Crc16(frame[..bodyEnd]) == Binary.U16BigEndian(frame[bodyEnd..]));
    }

    private static bool IsSideChannel(FlacChannelAssignment assignment, int channel) => assignment switch
    {
        FlacChannelAssignment.LeftSide => channel == 1,
        FlacChannelAssignment.SideRight => channel == 0,
        FlacChannelAssignment.MidSide => channel == 1,
        _ => false,
    };

    private enum Step
    {
        Done,
        RanOut,
        Malformed,
    }

    private static Step SkipSubframe(ref BitCursor bits, int blockSize, int depth)
    {
        if (!bits.TryRead(8, out var head))
        {
            return Step.RanOut;
        }

        if ((head & 0x80) != 0)
        {
            return Step.Malformed;
        }

        var type = (int)(head >> 1) & 0x3F;
        if ((head & 1) == 1)
        {
            // Wasted bits: one more than the run of zeros before a one.
            if (!bits.TrySkipUnary(depth, out var zeros))
            {
                return zeros < 0 ? Step.RanOut : Step.Malformed;
            }

            depth -= (int)zeros + 1;
            if (depth <= 0)
            {
                return Step.Malformed;
            }
        }

        switch (type)
        {
            case 0:
                return bits.TrySkip(depth) ? Step.Done : Step.RanOut;
            case 1:
                return bits.TrySkip((long)blockSize * depth) ? Step.Done : Step.RanOut;
            case >= 8 and <= 12:
            {
                var order = type - 8;
                return !bits.TrySkip((long)order * depth) ? Step.RanOut : SkipResidual(ref bits, blockSize, order);
            }

            case >= 32:
            {
                var order = type - 31;
                if (!bits.TrySkip((long)order * depth) || !bits.TryRead(4, out var precisionBits) || !bits.TryRead(5, out var shift))
                {
                    return Step.RanOut;
                }

                // A precision of 0b1111 and a negative shift are forbidden (RFC 9639, section 9.2.6).
                if (precisionBits == 0b1111 || (shift & 0x10) != 0)
                {
                    return Step.Malformed;
                }

                return !bits.TrySkip((long)order * (precisionBits + 1)) ? Step.RanOut : SkipResidual(ref bits, blockSize, order);
            }

            default:
                return Step.Malformed;
        }
    }

    /// <summary>The residual of a predicted subframe: partitions, each Rice-coded with its own parameter, or escaped.</summary>
    private static Step SkipResidual(ref BitCursor bits, int blockSize, int order)
    {
        if (!bits.TryRead(2, out var method) || !bits.TryRead(4, out var partitionOrder))
        {
            return Step.RanOut;
        }

        if (method > 1)
        {
            return Step.Malformed;
        }

        var parameterBits = method == 0 ? 4 : 5;
        var escape = (1u << parameterBits) - 1;
        var partitions = 1 << (int)partitionOrder;
        if (blockSize % partitions != 0 || (blockSize >> (int)partitionOrder) < order)
        {
            return Step.Malformed;
        }

        for (var p = 0; p < partitions; p++)
        {
            var samples = (blockSize >> (int)partitionOrder) - (p == 0 ? order : 0);
            if (!bits.TryRead(parameterBits, out var parameter))
            {
                return Step.RanOut;
            }

            if (parameter == escape)
            {
                if (!bits.TryRead(5, out var width) || !bits.TrySkip((long)samples * width))
                {
                    return Step.RanOut;
                }

                continue;
            }

            for (var s = 0; s < samples; s++)
            {
                if (!bits.TrySkipUnary(long.MaxValue, out _) || !bits.TrySkip(parameter))
                {
                    return Step.RanOut;
                }
            }
        }

        return Step.Done;
    }

    /// <summary>A position in a span of bytes, counted in bits from the first byte's high bit.</summary>
    private ref struct BitCursor(ReadOnlySpan<byte> bytes, long position)
    {
        private readonly ReadOnlySpan<byte> bytes = bytes;

        public long Position { get; private set; } = position;

        private readonly long Length => bytes.Length * 8L;

        public bool TrySkip(long count)
        {
            if (count > Length - Position)
            {
                return false;
            }

            Position += count;
            return true;
        }

        public bool TryRead(int count, out uint value)
        {
            value = 0;
            if (count > Length - Position)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                var bit = (bytes[(int)(Position >> 3)] >> (7 - (int)(Position & 7))) & 1;
                value = (value << 1) | (uint)bit;
                Position++;
            }

            return true;
        }

        /// <summary>
        /// Skips a run of zeros and the one that ends it, giving the zeros' count. Fails with a
        /// count of -1 where the bytes run out, or with the count where it exceeds <paramref name="limit"/>.
        /// </summary>
        public bool TrySkipUnary(long limit, out long zeros)
        {
            zeros = 0;
            while (Position < Length)
            {
                if ((Position & 7) == 0 && bytes[(int)(Position >> 3)] == 0)
                {
                    zeros += 8;
                    Position += 8;
                }
                else if (((bytes[(int)(Position >> 3)] >> (7 - (int)(Position & 7))) & 1) == 0)
                {
                    zeros++;
                    Position++;
                }
                else
                {
                    Position++;
                    return zeros <= limit;
                }

                if (zeros > limit)
                {
                    return false;
                }
            }

            zeros = -1;
            return false;
        }
    }
}
