using System.IO.Compression;
using System.Text;
using static Goro.Scratchpad.FileReading.Bin;

namespace Goro.Scratchpad.FileReading;

/// <summary>
/// Builds tag bytes by hand, as testing.md asks: never through a tag library, so that a reader is
/// tested against what a tagger could have written rather than against its own writer.
/// </summary>
public static class TagWriters
{
    // ---------------------------------------------------------------- Id3v2 text payloads

    /// <summary>A text frame's content: the encoding byte, then the values separated by NUL.</summary>
    public static byte[] Text(byte encoding, params string[] values) => TextWith(encoding, false, values);

    /// <summary>As <see cref="Text"/>, with a NUL after the last value too, as many taggers write.</summary>
    public static byte[] TextTerminated(byte encoding, params string[] values) => TextWith(encoding, true, values);

    static byte[] TextWith(byte encoding, bool terminate, string[] values)
    {
        var parts = new List<byte[]> { new[] { encoding } };
        for (var i = 0; i < values.Length; i++)
        {
            parts.Add(Encode(encoding, values[i]));
            if (i < values.Length - 1 || terminate)
            {
                parts.Add(Nul(encoding));
            }
        }

        return Concat(parts.ToArray());
    }

    public static byte[] Encode(byte encoding, string s) => encoding switch
    {
        0 => Latin1.GetBytes(s),
        1 => Concat([0xFF, 0xFE], Encoding.Unicode.GetBytes(s)),
        2 => Encoding.BigEndianUnicode.GetBytes(s),
        3 => Utf8.GetBytes(s),
        _ => Utf8.GetBytes(s),
    };

    public static byte[] Nul(byte encoding) => encoding is 1 or 2 ? [0, 0] : [0];

    /// <summary>TXXX / WXXX-shaped content: encoding, description, NUL, value.</summary>
    public static byte[] Described(byte encoding, string description, string value) =>
        Concat([encoding], Encode(encoding, description), Nul(encoding), Encode(encoding, value));

    /// <summary>COMM / USLT content: encoding, language, short description, NUL, text.</summary>
    public static byte[] Comment(byte encoding, string language, string description, string text) =>
        Concat([encoding], Ascii(language), Encode(encoding, description), Nul(encoding), Encode(encoding, text));

    /// <summary>A WXXX-free URL frame (W***): ISO-8859-1, no encoding byte.</summary>
    public static byte[] Url(string url) => Latin1.GetBytes(url);

    public static byte[] Apic(string mime, byte pictureType, string description, byte[] data) =>
        Concat([0], Latin1.GetBytes(mime), [0, pictureType], Latin1.GetBytes(description), [0], data);

    /// <summary>The v2.2 PIC frame: a three-letter image format instead of a MIME type.</summary>
    public static byte[] Pic(string format, byte pictureType, string description, byte[] data) =>
        Concat([0], Ascii(format), [pictureType], Latin1.GetBytes(description), [0], data);

    public static byte[] Priv(string owner, byte[] data) => Concat(Latin1.GetBytes(owner), [0], data);

    public static byte[] Popm(string email, byte rating, uint count) =>
        Concat(Latin1.GetBytes(email), [0, rating], U32BE(count));

    /// <summary>A tiny but real PNG (1x1, red), so that picture payloads are recognisable.</summary>
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    /// <summary>Picture-like binary that contains 0xFF followed by 0xE0+ and 0x00, so unsynchronisation changes it.</summary>
    public static readonly byte[] SyncBait = [0x01, 0xFF, 0xFB, 0x90, 0x44, 0xFF, 0x00, 0x12, 0xFF, 0xE3, 0x7F, 0xFF];

    // ---------------------------------------------------------------- Id3v2 frames and tags

    public sealed record Frame(string Id, byte[] Content)
    {
        /// <summary>Raw status and format flags, written as they are (v2.3/v2.4 only).</summary>
        public ushort Flags { get; init; }

        /// <summary>v2.4: unsynchronise this frame's data and set flag n.</summary>
        public bool Unsync { get; init; }

        /// <summary>v2.4: prepend a data length indicator and set flag p.</summary>
        public bool DataLength { get; init; }

        /// <summary>Compress with zlib, setting the format flag for the revision.</summary>
        public bool Compress { get; init; }

        /// <summary>Mark as encrypted with this method symbol; the content is written as it is.</summary>
        public byte? EncryptionMethod { get; init; }

        /// <summary>Overrides the size written in the frame header.</summary>
        public uint? SizeOverride { get; init; }

        /// <summary>v2.4: write the size as a plain big-endian integer, as iTunes once did.</summary>
        public bool PlainSize { get; init; }
    }

    public sealed record Id3v2Options
    {
        public int Padding { get; init; }
        public bool Unsync { get; init; }          // tag-level flag; v2.3 unsynchronises the whole body
        public bool ExtendedHeader { get; init; }
        public bool Footer { get; init; }          // v2.4
        public byte Revision { get; init; }
        public uint? SizeOverride { get; init; }
        public bool PlainTagSize { get; init; }    // size bytes not syncsafe
        public byte? MajorOverride { get; init; }
    }

    public static byte[] Id3v2(byte major, IEnumerable<Frame> frames, Id3v2Options? options = null)
    {
        options ??= new Id3v2Options();
        var body = new List<byte[]>();

        if (options.ExtendedHeader)
        {
            body.Add(major == 3
                // size (excluding itself) 6, flags 0, padding size
                ? Concat(U32BE(6), [0, 0], U32BE((uint)options.Padding))
                // size (including itself) 6, one flag byte, flags 0
                : Concat(Syncsafe(6), [1, 0]));
        }

        foreach (var f in frames)
        {
            body.Add(RenderFrame(major, f, options.Unsync && major == 4));
        }

        var frameBytes = Concat(body.ToArray());
        if (options.Unsync && major < 4)
        {
            frameBytes = Unsynchronise(frameBytes);
        }

        frameBytes = Concat(frameBytes, new byte[options.Padding]);

        byte flags = 0;
        if (options.Unsync)
        {
            flags |= 0x80;
        }

        if (options.ExtendedHeader)
        {
            flags |= 0x40;
        }

        if (options.Footer)
        {
            flags |= 0x10;
        }

        var size = options.SizeOverride ?? (uint)frameBytes.Length;
        var sizeBytes = options.PlainTagSize ? U32BE(size) : Syncsafe(size);
        var header = Concat(Ascii("ID3"), [options.MajorOverride ?? major, options.Revision, flags], sizeBytes);
        var footer = options.Footer ? Concat(Ascii("3DI"), [major, options.Revision, flags], sizeBytes) : [];
        return Concat(header, frameBytes, footer);
    }

    static byte[] RenderFrame(byte major, Frame f, bool tagUnsync)
    {
        var data = f.Content;
        if (major == 2)
        {
            return Concat(Ascii(f.Id), U24BE(f.SizeOverride ?? (uint)data.Length), data);
        }

        var flags = f.Flags;
        var prefix = new List<byte>();
        var decompressedSize = (uint)data.Length;

        if (f.Compress)
        {
            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                z.Write(data);
            }

            data = ms.ToArray();
            flags |= major == 3 ? (ushort)0x0080 : (ushort)0x0008;
        }

        if (f.EncryptionMethod is { } method)
        {
            flags |= major == 3 ? (ushort)0x0040 : (ushort)0x0004;
        }

        if (major == 3)
        {
            if (f.Compress)
            {
                prefix.AddRange(U32BE(decompressedSize));
            }

            if (f.EncryptionMethod is { } m3)
            {
                prefix.Add(m3);
            }
        }
        else
        {
            if (f.EncryptionMethod is { } m4)
            {
                prefix.Add(m4);
            }

            if (f.DataLength || f.Compress)
            {
                flags |= 0x0001;
                prefix.AddRange(Syncsafe(decompressedSize));
            }

            if (f.Unsync || tagUnsync)
            {
                flags |= 0x0002;
                data = Unsynchronise(data);
            }
        }

        var payload = Concat(prefix.ToArray(), data);
        var size = f.SizeOverride ?? (uint)payload.Length;
        var sizeBytes = major == 4 && !f.PlainSize ? Syncsafe(size) : U32BE(size);
        return Concat(Ascii(f.Id), sizeBytes, U16BE(flags), payload);
    }

    // ---------------------------------------------------------------- Id3v1

    public static byte[] Id3v1(string title, string artist, string album, string year, string comment, int? track, byte genre)
    {
        static byte[] Field(string s, int width) => Latin1.GetBytes(s).Concat(new byte[width]).Take(width).ToArray();

        var commentBytes = track is { } t
            ? Concat(Field(comment, 28), [0, (byte)t])
            : Field(comment, 30);
        return Concat(Ascii("TAG"), Field(title, 30), Field(artist, 30), Field(album, 30), Field(year, 4), commentBytes, [genre]);
    }

    // ---------------------------------------------------------------- APE

    public sealed record ApeItem(string Key, uint Flags, byte[] Value)
    {
        public static ApeItem TextItem(string key, params string[] values) =>
            new(key, 0, Utf8.GetBytes(string.Join('\0', values)));

        public static ApeItem Binary(string key, byte[] value) => new(key, 1 << 1, value);
        public static ApeItem Locator(string key, string url) => new(key, 2 << 1, Utf8.GetBytes(url));

        public uint? SizeOverride { get; init; }
    }

    public sealed record ApeOptions
    {
        public uint Version { get; init; } = 2000;
        public bool Header { get; init; } = true;
        public uint? ItemCountOverride { get; init; }
        public uint? TagSizeOverride { get; init; }
    }

    public static byte[] Ape(IEnumerable<ApeItem> items, ApeOptions? options = null)
    {
        options ??= new ApeOptions();
        var list = items.ToList();
        var itemBytes = Concat(list.Select(i => Concat(
            U32LE(i.SizeOverride ?? (uint)i.Value.Length), U32LE(i.Flags), Ascii(i.Key), [0], i.Value)).ToArray());
        var tagSize = options.TagSizeOverride ?? (uint)(itemBytes.Length + 32);
        var count = options.ItemCountOverride ?? (uint)list.Count;
        var hasHeader = options.Header && options.Version >= 2000;
        var baseFlags = hasHeader ? 0x80000000u : 0;

        byte[] Block(uint flags) => Concat(Ascii("APETAGEX"), U32LE(options.Version), U32LE(tagSize), U32LE(count), U32LE(flags), new byte[8]);

        return Concat(hasHeader ? Block(baseFlags | 0x20000000) : [], itemBytes, Block(baseFlags));
    }

    // ---------------------------------------------------------------- Lyrics3v2

    public static byte[] Lyrics3v2(string lyrics)
    {
        var fields = Concat(Ascii("IND00002"), Ascii("10"), Ascii($"LYR{lyrics.Length:D5}"), Latin1.GetBytes(lyrics));
        var content = Concat(Ascii("LYRICSBEGIN"), fields);
        return Concat(content, Ascii($"{content.Length:D6}"), Ascii("LYRICS200"));
    }

    // ---------------------------------------------------------------- Vorbis comments

    public sealed record VorbisCommentOptions
    {
        public uint? CountOverride { get; init; }
        public uint? VendorLengthOverride { get; init; }

        /// <summary>Overrides the length field of the comment at this index.</summary>
        public (int Index, uint Length)? CommentLengthOverride { get; init; }
    }

    /// <summary>The comment structure shared by all three containers, without any prefix or framing bit.</summary>
    public static byte[] VorbisComment(string vendor, IReadOnlyList<byte[]> comments, VorbisCommentOptions? options = null)
    {
        options ??= new VorbisCommentOptions();
        var vendorBytes = Utf8.GetBytes(vendor);
        var parts = new List<byte[]>
        {
            U32LE(options.VendorLengthOverride ?? (uint)vendorBytes.Length),
            vendorBytes,
            U32LE(options.CountOverride ?? (uint)comments.Count),
        };
        for (var i = 0; i < comments.Count; i++)
        {
            var length = options.CommentLengthOverride is { } o && o.Index == i ? o.Length : (uint)comments[i].Length;
            parts.Add(U32LE(length));
            parts.Add(comments[i]);
        }

        return Concat(parts.ToArray());
    }

    public static byte[] Comment(string key, string value) => Utf8.GetBytes($"{key}={value}");

    public static byte[] VorbisCommentPacket(byte[] structure, bool framingBit = true) =>
        Concat([3], Ascii("vorbis"), structure, framingBit ? [1] : []);

    public static byte[] OpusTagsPacket(byte[] structure) => Concat(Ascii("OpusTags"), structure);

    /// <summary>A FLAC PICTURE block's content, which METADATA_BLOCK_PICTURE base64-encodes in Ogg.</summary>
    public static byte[] FlacPicture(byte[] png) => Concat(
        U32BE(3), U32BE(9), Ascii("image/png"), U32BE(5), Ascii("cover"),
        U32BE(1), U32BE(1), U32BE(24), U32BE(0), U32BE((uint)png.Length), png);
}
