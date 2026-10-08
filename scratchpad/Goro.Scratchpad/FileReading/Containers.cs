using static Goro.Scratchpad.FileReading.Bin;

namespace Goro.Scratchpad.FileReading;

// ==================================================================================== MPEG audio

/// <summary>A parsed MPEG audio frame header. Version is 1, 2 or 25 (for 2.5).</summary>
public readonly record struct MpegHeader(
    int Version, int Layer, bool Crc, int BitrateKbps, int SampleRate, bool Padding, int ChannelMode, int FrameLength)
{
    public int SamplesPerFrame => Layer switch { 1 => 384, 2 => 1152, _ => Version == 1 ? 1152 : 576 };
    public bool Mono => ChannelMode == 3;

    /// <summary>The mean frame length at this bitrate: what the padding bit keeps a constant-bitrate stream to.</summary>
    public double AverageFrameLength => (Layer == 1 ? 48.0 : Layer == 3 && Version != 1 ? 72.0 : 144.0) * BitrateKbps * 1000 / SampleRate;

    /// <summary>Bytes from the frame start to the Xing/Info tag: header, CRC, side info.</summary>
    public int XingOffset => 4 + (Crc ? 2 : 0) + (Version == 1 ? (Mono ? 17 : 32) : (Mono ? 9 : 17));

    static readonly int[][] Bitrates =
    [
        [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448], // V1 L1
        [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384],    // V1 L2
        [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320],     // V1 L3
        [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256],    // V2 L1
        [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160],         // V2 L2, L3
    ];

    /// <summary>Parses the four bytes at <paramref name="at"/>; free-format frames get length 0.</summary>
    public static bool TryParse(ReadOnlySpan<byte> b, int at, out MpegHeader h)
    {
        h = default;
        if (at < 0 || at + 4 > b.Length || b[at] != 0xFF || (b[at + 1] & 0xE0) != 0xE0)
        {
            return false;
        }

        var versionBits = (b[at + 1] >> 3) & 3;
        var layerBits = (b[at + 1] >> 1) & 3;
        var bitrateIndex = b[at + 2] >> 4;
        var rateIndex = (b[at + 2] >> 2) & 3;
        if (versionBits == 1 || layerBits == 0 || bitrateIndex == 15 || rateIndex == 3)
        {
            return false;
        }

        var version = versionBits switch { 3 => 1, 2 => 2, _ => 25 };
        var layer = 4 - layerBits;
        int[] rates = version switch { 1 => [44100, 48000, 32000], 2 => [22050, 24000, 16000], _ => [11025, 12000, 8000] };
        var sampleRate = rates[rateIndex];
        var table = version == 1 ? layer - 1 : (layer == 1 ? 3 : 4);
        var kbps = Bitrates[table][bitrateIndex];
        var padding = ((b[at + 2] >> 1) & 1) == 1;
        var length = kbps == 0
            ? 0
            : layer == 1
                ? (12 * kbps * 1000 / sampleRate + (padding ? 1 : 0)) * 4
                : (layer == 3 && version != 1 ? 72 : 144) * kbps * 1000 / sampleRate + (padding ? 1 : 0);
        h = new MpegHeader(version, layer, (b[at + 1] & 1) == 0, kbps, sampleRate, padding, b[at + 3] >> 6, length);
        return true;
    }

    /// <summary>Whether two headers belong to the same stream: what a sync search confirms against.</summary>
    public bool Matches(MpegHeader o) => o.Version == Version && o.Layer == Layer && o.SampleRate == SampleRate;
}

// ==================================================================================== Ogg

public sealed record OggPage(
    long Offset, byte Flags, long Granule, uint Serial, uint Sequence, uint Crc, byte[] Lacing, byte[] Body, bool CrcOk)
{
    public int Length => 27 + Lacing.Length + Body.Length;
    public bool Continued => (Flags & 1) != 0;
    public bool Bos => (Flags & 2) != 0;
    public bool Eos => (Flags & 4) != 0;
}

public static class Ogg
{
    /// <summary>Parses pages at <paramref name="at"/>, or null if no well-formed page header is there.</summary>
    public static OggPage? TryParsePage(byte[] f, long at)
    {
        if (at + 27 > f.Length || !StartsWith(f, (int)at, "OggS") || f[at + 4] != 0)
        {
            return null;
        }

        var i = (int)at;
        int segments = f[i + 26];
        if (i + 27 + segments > f.Length)
        {
            return null;
        }

        var lacing = f.AsSpan(i + 27, segments).ToArray();
        var bodyLength = lacing.Sum(x => x);
        if (i + 27 + segments + bodyLength > f.Length)
        {
            return null;
        }

        var body = f.AsSpan(i + 27 + segments, bodyLength).ToArray();
        var crc = ReadU32LE(f, i + 22);
        var copy = f.AsSpan(i, 27 + segments + bodyLength).ToArray();
        copy[22] = copy[23] = copy[24] = copy[25] = 0;
        var ok = OggCrc(copy) == crc;
        return new OggPage(at, f[i + 5], (long)ReadU64LE(f, i + 6), ReadU32LE(f, i + 14), ReadU32LE(f, i + 18), crc, lacing, body, ok);
    }

    /// <summary>Every page in order, skipping forward to the next capture pattern where a page does not parse.</summary>
    public static List<OggPage> Pages(byte[] f)
    {
        var pages = new List<OggPage>();
        long at = 0;
        while (at < f.Length)
        {
            if (TryParsePage(f, at) is { } p)
            {
                pages.Add(p);
                at += p.Length;
                continue;
            }

            var next = f.AsSpan((int)at + 1).IndexOf("OggS"u8);
            if (next < 0)
            {
                break;
            }

            at += next + 1;
        }

        return pages;
    }

    public static byte[] RenderPage(byte flags, long granule, uint serial, uint sequence, byte[] lacing, byte[] body)
    {
        var page = Concat(Ascii("OggS"), [0, flags], BitConverter.GetBytes(granule), U32LE(serial), U32LE(sequence),
            U32LE(0), [(byte)lacing.Length], lacing, body);
        U32LE(OggCrc(page)).CopyTo(page, 22);
        return page;
    }

    /// <summary>Lays packets out on pages as libogg would, flushing after the last one.</summary>
    public static List<byte[]> Paginate(IReadOnlyList<byte[]> packets, uint serial, uint firstSequence, long granule)
    {
        var pages = new List<byte[]>();
        var lacing = new List<byte>();
        var body = new List<byte>();
        var continued = false;
        var completed = false;
        var sequence = firstSequence;

        void Flush()
        {
            pages.Add(RenderPage((byte)(continued ? 1 : 0), completed ? granule : -1, serial, sequence++, lacing.ToArray(), body.ToArray()));
            lacing.Clear();
            body.Clear();
            completed = false;
        }

        foreach (var packet in packets)
        {
            var offset = 0;
            while (true)
            {
                // A packet whose length is a multiple of 255 ends with a zero-length segment.
                var segment = Math.Min(255, packet.Length - offset);
                lacing.Add((byte)segment);
                body.AddRange(packet.AsSpan(offset, segment).ToArray());
                offset += segment;
                var packetDone = segment < 255;
                completed |= packetDone;
                if (lacing.Count == 255)
                {
                    Flush();
                    continued = !packetDone;
                }

                if (packetDone)
                {
                    break;
                }
            }
        }

        if (lacing.Count > 0)
        {
            Flush();
        }

        return pages;
    }

    /// <summary>The packets of one logical stream, each with the index of the page it ends on.</summary>
    public static List<(byte[] Packet, int EndPage)> Packets(IReadOnlyList<OggPage> pages, uint serial)
    {
        var result = new List<(byte[], int)>();
        var current = new List<byte>();
        for (var p = 0; p < pages.Count; p++)
        {
            var page = pages[p];
            if (page.Serial != serial)
            {
                continue;
            }

            var at = 0;
            foreach (var l in page.Lacing)
            {
                current.AddRange(page.Body.AsSpan(at, l).ToArray());
                at += l;
                if (l < 255)
                {
                    result.Add((current.ToArray(), p));
                    current.Clear();
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Rewrites a single-stream Ogg file with its comment packet (the second header packet) replaced,
    /// renumbering every later page as a tagger has to when the comment changes size.
    /// </summary>
    public static byte[] ReplaceComment(byte[] file, byte[] newComment, int headerPackets)
    {
        var pages = Pages(file);
        var serial = pages[0].Serial;
        var packets = Packets(pages, serial);
        var lastHeaderPage = packets[headerPackets - 1].EndPage;
        var headers = packets.Take(headerPackets).Select(p => p.Packet).ToList();
        headers[1] = newComment;

        var output = new List<byte[]> { file.AsSpan(0, pages[0].Length).ToArray() };
        var rest = Paginate(headers.Skip(1).ToList(), serial, 1, 0);
        output.AddRange(rest);
        var sequence = (uint)(1 + rest.Count);
        foreach (var page in pages.Skip(lastHeaderPage + 1))
        {
            output.Add(RenderPage(page.Flags, page.Granule, page.Serial, sequence++, page.Lacing, page.Body));
        }

        return Concat(output.ToArray());
    }
}

// ==================================================================================== FLAC

public sealed record FlacBlock(int Type, byte[] Data, bool Last, long Offset);

public static class Flac
{
    public const int StreamInfo = 0, Padding = 1, Application = 2, SeekTable = 3, VorbisComment = 4, CueSheet = 5, Picture = 6;

    /// <summary>The metadata blocks of a well-formed file and the offset at which the frames start.</summary>
    public static (List<FlacBlock> Blocks, int AudioOffset) Parse(byte[] f, int start = 0)
    {
        if (!StartsWith(f, start, "fLaC"))
        {
            throw new InvalidDataException("no fLaC marker");
        }

        var blocks = new List<FlacBlock>();
        var at = start + 4;
        while (true)
        {
            var last = (f[at] & 0x80) != 0;
            var type = f[at] & 0x7F;
            var length = (int)ReadU24BE(f, at + 1);
            blocks.Add(new FlacBlock(type, f.AsSpan(at + 4, length).ToArray(), last, at));
            at += 4 + length;
            if (last)
            {
                return (blocks, at);
            }
        }
    }

    public static byte[] BlockBytes(int type, byte[] data, bool last, uint? lengthOverride = null) =>
        Concat([(byte)((last ? 0x80 : 0) | type)], U24BE(lengthOverride ?? (uint)data.Length), data);

    /// <summary>Builds a file from blocks, marking the last one, followed by the given frames.</summary>
    public static byte[] Build(IReadOnlyList<(int Type, byte[] Data)> blocks, byte[] audio, bool markLast = true) =>
        Concat(
            Ascii("fLaC"),
            Concat(blocks.Select((b, i) => BlockBytes(b.Type, b.Data, markLast && i == blocks.Count - 1)).ToArray()),
            audio);
}

/// <summary>A FLAC frame header that passed its CRC-8. Number is a frame number (fixed blocking) or a sample number (variable).</summary>
public readonly record struct FlacFrameHeader(bool VariableBlocking, ulong Number, int BlockSize, int SampleRate, int Length)
{
    public static bool TryParse(ReadOnlySpan<byte> f, int at, out FlacFrameHeader h)
    {
        h = default;
        if (at + 6 > f.Length || f[at] != 0xFF || (f[at + 1] & 0xFE) != 0xF8)
        {
            return false;
        }

        var blockCode = f[at + 2] >> 4;
        var rateCode = f[at + 2] & 0x0F;
        var channels = f[at + 3] >> 4;
        var sizeCode = (f[at + 3] >> 1) & 7;
        if (blockCode == 0 || rateCode == 15 || channels > 10 || sizeCode == 3 || (f[at + 3] & 1) != 0)
        {
            return false;
        }

        // The coded number: UTF-8-like, up to 7 bytes.
        var i = at + 4;
        int lead = f[i];
        int extra;
        ulong value;
        if (lead < 0x80) { extra = 0; value = (ulong)lead; }
        else if ((lead & 0xE0) == 0xC0) { extra = 1; value = (ulong)(lead & 0x1F); }
        else if ((lead & 0xF0) == 0xE0) { extra = 2; value = (ulong)(lead & 0x0F); }
        else if ((lead & 0xF8) == 0xF0) { extra = 3; value = (ulong)(lead & 0x07); }
        else if ((lead & 0xFC) == 0xF8) { extra = 4; value = (ulong)(lead & 0x03); }
        else if ((lead & 0xFE) == 0xFC) { extra = 5; value = (ulong)(lead & 0x01); }
        else if (lead == 0xFE) { extra = 6; value = 0; }
        else { return false; }

        if (i + 1 + extra > f.Length)
        {
            return false;
        }

        for (var k = 1; k <= extra; k++)
        {
            if ((f[i + k] & 0xC0) != 0x80)
            {
                return false;
            }

            value = (value << 6) | (ulong)(f[i + k] & 0x3F);
        }

        i += 1 + extra;
        var blockSize = blockCode switch
        {
            1 => 192,
            >= 2 and <= 5 => 576 << (blockCode - 2),
            >= 8 => 256 << (blockCode - 8),
            _ => -1,
        };
        if (blockCode == 6) { if (i + 1 > f.Length) return false; blockSize = f[i] + 1; i += 1; }
        if (blockCode == 7) { if (i + 2 > f.Length) return false; blockSize = (f[i] << 8 | f[i + 1]) + 1; i += 2; }

        var rate = rateCode switch
        {
            0 => 0, 1 => 88200, 2 => 176400, 3 => 192000, 4 => 8000, 5 => 16000, 6 => 22050, 7 => 24000,
            8 => 32000, 9 => 44100, 10 => 48000, 11 => 96000, _ => -1,
        };
        if (rateCode == 12) { if (i + 1 > f.Length) return false; rate = f[i] * 1000; i += 1; }
        if (rateCode is 13 or 14) { if (i + 2 > f.Length) return false; rate = (f[i] << 8 | f[i + 1]) * (rateCode == 14 ? 10 : 1); i += 2; }

        if (i + 1 > f.Length || Bin.Crc8(f[at..i]) != f[i])
        {
            return false;
        }

        h = new FlacFrameHeader((f[at + 1] & 1) == 1, value, blockSize, rate, i + 1 - at);
        return true;
    }
}
