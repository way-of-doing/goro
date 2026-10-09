using System.Text;

namespace Goro.Tests.TestSupport;

/// <summary>APE and Id3v1 tags, byte by byte, for the shapes the fixture corpus does not hold.</summary>
internal static class TagBytes
{
    public sealed record ApeItem(string Key, byte[] Value, bool Binary = false);

    public static ApeItem ApeText(string key, params string[] values) => new(key, Encoding.UTF8.GetBytes(string.Join('\0', values)));

    /// <summary>An APEv2 tag with a header and a footer.</summary>
    public static byte[] Ape(params ApeItem[] items)
    {
        using var body = new MemoryStream();
        foreach (var item in items)
        {
            body.Write(LittleEndian(item.Value.Length));
            body.Write(LittleEndian(item.Binary ? 1 << 1 : 0));
            body.Write(Encoding.ASCII.GetBytes(item.Key));
            body.WriteByte(0);
            body.Write(item.Value);
        }

        var size = (int)body.Length + 32;
        byte[] Preamble(uint flags) =>
            [.. "APETAGEX"u8, .. LittleEndian(2000), .. LittleEndian(size), .. LittleEndian(items.Length), .. LittleEndian((int)flags), .. new byte[8]];
        return [.. Preamble(0xA0000000), .. body.ToArray(), .. Preamble(0x80000000)];
    }

    /// <summary>
    /// An Id3v1 tag: text fields padded with NUL to their widths, the year as given (four bytes),
    /// and a track byte only when <paramref name="track"/> is given, which makes it v1.1.
    /// </summary>
    public static byte[] Id3v1(string title = "", string artist = "", string album = "", string year = "", byte? track = null, byte genre = 255, byte padding = 0)
    {
        var tag = new byte[128];
        tag.AsSpan().Fill(padding);
        "TAG"u8.CopyTo(tag);
        Field(tag, 3, 30, title);
        Field(tag, 33, 30, artist);
        Field(tag, 63, 30, album);
        Field(tag, 93, 4, year);
        tag.AsSpan(97, 30).Fill(padding);
        if (track is { } number)
        {
            tag[125] = 0;
            tag[126] = number;
        }

        tag[127] = genre;
        return tag;
    }

    private static void Field(byte[] tag, int at, int width, string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        bytes.AsSpan(0, Math.Min(width, bytes.Length)).CopyTo(tag.AsSpan(at));
    }

    private static byte[] LittleEndian(int value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
}
