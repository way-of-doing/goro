using System.Security.Cryptography;
using System.Text;

namespace Goro.Scratchpad.FormatSupport;

// Does the byte range TagLibSharp calls invariant survive a tag edit? The hasher in src/ hashes
// [InvariantStartPosition, InvariantEndPosition), so this decides whether a format can be given
// the audio hash guarantee of docs/vision.md as things stand. Each file is built by hand, like
// SyntheticMp3Builder in the tests, since no encoder is needed to ask the question: the audio
// payloads are random bytes, and only the container structure has to be right.
//
// Each file is tagged twice: once with a title too long for the pages or blocks it started with,
// then back to a short one. The hash must be the same at all three points. Recorded in the log of
// docs/directions/file-discovery-scope.md (2026-10-05): stable for MP3; not for FLAC, whose range
// starts at byte 0 and so takes in the metadata blocks; and not for Ogg once the comment grows past
// its pages, when TagLib renumbers every later page and changes its sequence number and CRC.
public static class HashStabilityProbe
{
    static readonly Random Rng = new(1);

    public static void Run()
    {
        var dir = Directory.CreateTempSubdirectory("goro-probe-");
        try
        {
            (string Name, byte[] Data)[] files =
            [
                ("a.mp3", Mp3()), ("a.flac", Flac()), ("a.ogg", Vorbis()), ("a.opus", Opus()),
                // The reader is chosen by extension, the codec by the stream: both of these read.
                ("opus-in.ogg", Opus()), ("vorbis-in.opus", Vorbis()),
            ];
            foreach (var (name, data) in files)
            {
                var path = Path.Combine(dir.FullName, name);
                File.WriteAllBytes(path, data);
                Probe(name, path);
            }
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    static void Probe(string name, string path)
    {
        try
        {
            using (var f = TagLib.File.Create(path))
            {
                Console.WriteLine($"{name}: {f.GetType().FullName}, mime {f.MimeType}, {f.Properties.Description}, {f.Properties.Duration}");
            }

            var before = Hash(path);
            Retitle(path, new string('t', 200_000));
            var grown = Hash(path);
            Retitle(path, "x");
            var shrunk = Hash(path);

            Console.WriteLine($"  before {before}\n  grown  {grown}\n  shrunk {shrunk}");
            Console.WriteLine($"  stable: {before.Digest == grown.Digest && grown.Digest == shrunk.Digest}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    static void Retitle(string path, string title)
    {
        using var f = TagLib.File.Create(path);
        f.Tag.Title = title;
        f.Save();
    }

    sealed record RangeHash(long Start, long End, long Length, string Digest)
    {
        public override string ToString() => $"[{Start}, {End}) of {Length}: {Digest}";
    }

    static RangeHash Hash(string path)
    {
        long start, end;
        using (var f = TagLib.File.Create(path))
        {
            start = f.InvariantStartPosition;
            end = f.InvariantEndPosition;
        }

        var bytes = File.ReadAllBytes(path);
        var digest = Convert.ToHexString(MD5.HashData(bytes.AsSpan((int)start, (int)(end - start))));
        return new RangeHash(start, end, bytes.Length, digest.ToLowerInvariant()[..12]);
    }

    // ---- MP3: twenty MPEG-1 Layer III frames, 128 kbps, 44.1 kHz, mono, no tags.

    static byte[] Mp3()
    {
        var frame = new byte[417];
        new byte[] { 0xFF, 0xFB, 0x90, 0xC0 }.CopyTo(frame, 0);
        return Concat(Enumerable.Repeat(frame, 20).ToArray());
    }

    // ---- FLAC: STREAMINFO, an empty Vorbis comment, then one frame header and noise.

    static byte[] Flac()
    {
        // Block sizes 4096/4096, frame sizes unknown, 44100 Hz, 2 channels, 16 bits, 88200 samples.
        var streamInfo = new byte[34];
        streamInfo[0] = 0x10;
        streamInfo[2] = 0x10;
        var packed = (44100UL << 44) | (1UL << 41) | (15UL << 36) | 88200UL;
        for (var i = 0; i < 8; i++)
        {
            streamInfo[10 + i] = (byte)(packed >> (56 - 8 * i));
        }

        var comment = Concat(Le32(5), "probe"u8.ToArray(), Le32(0));
        return Concat("fLaC"u8.ToArray(),
            BlockHeader(last: false, type: 0, streamInfo.Length), streamInfo,
            BlockHeader(last: true, type: 4, comment.Length), comment,
            [0xFF, 0xF8, 0x69, 0x08], Noise(1000));
    }

    static byte[] BlockHeader(bool last, int type, int length) =>
        [(byte)((last ? 0x80 : 0) | type), (byte)(length >> 16), (byte)(length >> 8), (byte)length];

    // ---- Ogg: header packets on the first two pages, as encoders lay them out, then audio pages.

    static byte[] Vorbis()
    {
        var identification = Concat([1], "vorbis"u8.ToArray(), Le32(0), [2], Le32(44100),
            Le32(0), Le32(128000), Le32(0), [0xB8], [1]);
        var comment = Concat([3], "vorbis"u8.ToArray(), Le32(5), "probe"u8.ToArray(), Le32(0), [1]);
        var setup = Concat([5], "vorbis"u8.ToArray(), Noise(40));
        return Concat(
            OggPage(0x02, 0, 0, identification),
            OggPage(0x00, 0, 1, comment, setup),
            OggPage(0x00, 44100, 2, Noise(200), Noise(200)),
            OggPage(0x04, 88200, 3, Noise(200)));
    }

    static byte[] Opus()
    {
        var head = Concat("OpusHead"u8.ToArray(), [1, 2], BitConverter.GetBytes((ushort)312), Le32(48000), [0, 0], [0]);
        var tags = Concat("OpusTags"u8.ToArray(), Le32(5), "probe"u8.ToArray(), Le32(0));
        return Concat(
            OggPage(0x02, 0, 0, head),
            OggPage(0x00, 0, 1, tags),
            OggPage(0x00, 48000, 2, Noise(200), Noise(200)),
            OggPage(0x04, 96312, 3, Noise(200)));
    }

    static byte[] OggPage(byte headerType, long granule, int sequence, params byte[][] packets)
    {
        var lacing = new List<byte>();
        var body = new MemoryStream();
        foreach (var packet in packets)
        {
            var n = packet.Length;
            for (; n >= 255; n -= 255)
            {
                lacing.Add(255);
            }

            lacing.Add((byte)n);
            body.Write(packet);
        }

        var page = new MemoryStream();
        var w = new BinaryWriter(page);
        w.Write("OggS"u8.ToArray());
        w.Write((byte)0);
        w.Write(headerType);
        w.Write(granule);
        w.Write(1234); // serial number
        w.Write(sequence);
        w.Write(0u); // CRC, filled in below
        w.Write((byte)lacing.Count);
        w.Write(lacing.ToArray());
        w.Write(body.ToArray());

        var bytes = page.ToArray();
        BitConverter.GetBytes(OggCrc(bytes)).CopyTo(bytes, 22);
        return bytes;
    }

    // CRC-32 with polynomial 0x04C11DB7, no reflection, zero initial value, over the page with its
    // CRC field zeroed.
    static uint OggCrc(byte[] page)
    {
        uint crc = 0;
        foreach (var b in page)
        {
            crc ^= (uint)b << 24;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
            }
        }

        return crc;
    }

    static byte[] Noise(int length)
    {
        var bytes = new byte[length];
        Rng.NextBytes(bytes);
        return bytes;
    }

    static byte[] Le32(int value) => BitConverter.GetBytes(value);

    static byte[] Concat(params byte[][] parts)
    {
        var stream = new MemoryStream();
        foreach (var part in parts)
        {
            stream.Write(part);
        }

        return stream.ToArray();
    }
}
