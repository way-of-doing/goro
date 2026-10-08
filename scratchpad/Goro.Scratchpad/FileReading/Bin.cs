using System.Text;

namespace Goro.Scratchpad.FileReading;

/// <summary>Byte-level helpers shared by the corpus builder and the prototype readers.</summary>
public static class Bin
{
    public static readonly Encoding Latin1 = Encoding.Latin1;
    public static readonly Encoding Utf8 = new UTF8Encoding(false);
    public static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    public static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    public static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var at = 0;
        foreach (var p in parts)
        {
            p.CopyTo(result, at);
            at += p.Length;
        }

        return result;
    }

    public static byte[] U32BE(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
    public static byte[] U24BE(uint v) => [(byte)(v >> 16), (byte)(v >> 8), (byte)v];
    public static byte[] U16BE(uint v) => [(byte)(v >> 8), (byte)v];
    public static byte[] U32LE(uint v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];

    public static byte[] Syncsafe(uint v) =>
        [(byte)((v >> 21) & 0x7F), (byte)((v >> 14) & 0x7F), (byte)((v >> 7) & 0x7F), (byte)(v & 0x7F)];

    public static uint ReadU32BE(ReadOnlySpan<byte> b, int at) =>
        (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);

    public static uint ReadU24BE(ReadOnlySpan<byte> b, int at) => (uint)(b[at] << 16 | b[at + 1] << 8 | b[at + 2]);
    public static uint ReadU16BE(ReadOnlySpan<byte> b, int at) => (uint)(b[at] << 8 | b[at + 1]);

    public static uint ReadU32LE(ReadOnlySpan<byte> b, int at) =>
        (uint)(b[at] | b[at + 1] << 8 | b[at + 2] << 16 | b[at + 3] << 24);

    public static ulong ReadU64LE(ReadOnlySpan<byte> b, int at) =>
        ReadU32LE(b, at) | (ulong)ReadU32LE(b, at + 4) << 32;

    public static uint ReadSyncsafe(ReadOnlySpan<byte> b, int at) =>
        (uint)((b[at] & 0x7F) << 21 | (b[at + 1] & 0x7F) << 14 | (b[at + 2] & 0x7F) << 7 | (b[at + 3] & 0x7F));

    public static bool IsSyncsafe(ReadOnlySpan<byte> b, int at) =>
        ((b[at] | b[at + 1] | b[at + 2] | b[at + 3]) & 0x80) == 0;

    public static bool StartsWith(ReadOnlySpan<byte> b, int at, string ascii)
    {
        if (at < 0 || at + ascii.Length > b.Length)
        {
            return false;
        }

        for (var i = 0; i < ascii.Length; i++)
        {
            if (b[at + i] != ascii[i])
            {
                return false;
            }
        }

        return true;
    }

    public static string Hex(ReadOnlySpan<byte> b, int max = 24) =>
        Convert.ToHexString(b.Length > max ? b[..max] : b).ToLowerInvariant() + (b.Length > max ? $"..(+{b.Length - max})" : "");

    /// <summary>Id3v2 unsynchronisation: a zero byte after every 0xFF that precedes 0x00 or 0xE0 and above, and after a final 0xFF.</summary>
    public static byte[] Unsynchronise(ReadOnlySpan<byte> data)
    {
        var o = new List<byte>(data.Length + 16);
        for (var i = 0; i < data.Length; i++)
        {
            o.Add(data[i]);
            if (data[i] == 0xFF && (i + 1 == data.Length || data[i + 1] >= 0xE0 || data[i + 1] == 0x00))
            {
                o.Add(0x00);
            }
        }

        return o.ToArray();
    }

    public static byte[] Resynchronise(ReadOnlySpan<byte> data)
    {
        var o = new List<byte>(data.Length);
        for (var i = 0; i < data.Length; i++)
        {
            o.Add(data[i]);
            if (data[i] == 0xFF && i + 1 < data.Length && data[i + 1] == 0x00)
            {
                i++;
            }
        }

        return o.ToArray();
    }

    // Ogg: CRC-32, polynomial 0x04C11DB7, no reflection, initial value 0, no final xor.
    static readonly uint[] OggTable = MakeOggTable();

    static uint[] MakeOggTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var r = i << 24;
            for (var j = 0; j < 8; j++)
            {
                r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
            }

            t[i] = r;
        }

        return t;
    }

    public static uint OggCrc(ReadOnlySpan<byte> data)
    {
        uint crc = 0;
        foreach (var b in data)
        {
            crc = (crc << 8) ^ OggTable[((crc >> 24) & 0xFF) ^ b];
        }

        return crc;
    }

    // FLAC frame header CRC-8, polynomial x^8 + x^2 + x + 1.
    public static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
            }
        }

        return crc;
    }

    // The LAME tag's CRC-16/ARC (reflected 0x8005, initial value 0).
    public static ushort Crc16Arc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
            }
        }

        return crc;
    }

    // FLAC frame footer CRC-16, polynomial x^16 + x^15 + x^2 + 1.
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x8005) : (ushort)(crc << 1);
            }
        }

        return crc;
    }
}
