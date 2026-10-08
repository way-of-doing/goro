using System.IO.Compression;
using System.Text;
using static Goro.Scratchpad.FileReading.Bin;

namespace Goro.Scratchpad.FileReading;

/// <summary>
/// One datum as a reader found it. <see cref="Stored"/> is the bytes as they sit in the file;
/// <see cref="Content"/> is what the format says the datum is once its own encodings are undone
/// (unsynchronisation, compression), or null where they could not be. <see cref="Values"/> is the
/// text, one entry per value the format records, or null where there is no text or it would not decode.
/// </summary>
public sealed record RawField(string Tag, string Key, string? Description, byte[] Stored, byte[]? Content, string[]? Values, string? Problem = null)
{
    /// <summary>A quirk applied to read this field (see docs/design/quirks.md), or a choice the specification left open: for audit.</summary>
    public string? Quirk { get; init; }
}

/// <summary>
/// What a tag reader made of one tag: its fields, and the first point at which it lost the structure,
/// if any. A tag that is not <see cref="IsSource"/> was read for its extent and damage only: a second
/// Id3v2 tag, for instance.
/// </summary>
public sealed record RawTag(string Tag, long Offset, long Length, List<RawField> Fields, string? StructureProblem = null)
{
    public bool IsSource { get; init; } = true;
}

/// <summary>Prototype readers that give tag data as recorded and never throw on damaged input.</summary>
public static class TagReaders
{
    // ============================================================================ Id3v2

    static bool IsFrameIdChar(byte b) => b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9';

    static bool IsFrameId(ReadOnlySpan<byte> b, int at, int length)
    {
        if (at + length > b.Length)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            if (!IsFrameIdChar(b[at + i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the Id3v2 tag whose header is at <paramref name="at"/>; null if there is no header there.</summary>
    public static RawTag? Id3v2(byte[] f, int at)
    {
        if (!StartsWith(f, at, "ID3") || at + 10 > f.Length)
        {
            return null;
        }

        var major = f[at + 3];
        var flags = f[at + 5];
        var tagName = $"id3v2.{major}";
        var fields = new List<RawField>();
        if (major is < 2 or > 4)
        {
            return new RawTag(tagName, at, 10, fields, $"unknown major version {major}");
        }

        if (!IsSyncsafe(f, at + 6))
        {
            return new RawTag(tagName, at, 10, fields, "tag size is not a syncsafe integer");
        }

        var size = (int)ReadSyncsafe(f, at + 6);
        var footer = major == 4 && (flags & 0x10) != 0 ? 10 : 0;
        var declaredEnd = at + 10 + size;
        string? problem = null;
        if (declaredEnd > f.Length)
        {
            problem = $"tag size {size} runs past the end of the file";
        }

        var body = f.AsSpan(at + 10, Math.Min(size, f.Length - at - 10)).ToArray();
        var tagUnsync = (flags & 0x80) != 0;
        if (tagUnsync && major < 4)
        {
            body = Resynchronise(body);
        }

        var pos = 0;
        if ((flags & 0x40) != 0 && major >= 3)
        {
            // v2.3: size excludes itself; v2.4: syncsafe size includes itself.
            var ext = body.Length < 4 ? -1 : major == 3 ? ReadU32BE(body, 0) + 4L : ReadSyncsafe(body, 0);
            if (ext < 6 || ext > body.Length)
            {
                problem ??= "extended header runs past the end of the tag";
                pos = body.Length;
            }
            else
            {
                pos = (int)ext;
            }
        }

        var idLength = major == 2 ? 3 : 4;
        var headerLength = major == 2 ? 6 : 10;

        // Frame sizes in v2.4 are syncsafe, but some taggers wrote plain integers. Decide once for the
        // tag, as TagLib and mutagen do: plain if syncsafe walking does not land on frame boundaries
        // and plain walking does.
        var plainSizes = major == 4 && !WalksCleanly(body, pos, syncsafe: true) && WalksCleanly(body, pos, syncsafe: false);

        while (pos + headerLength <= body.Length)
        {
            if (body[pos] == 0)
            {
                break; // padding
            }

            if (!IsFrameId(body, pos, idLength))
            {
                // An illegal identifier whose size lands on another frame, the padding or the end of
                // the tag is stepped over, as the size field exists for (docs/design/quirks.md).
                if (major > 2 && StepsCleanly(body, pos, major == 4 && !plainSizes) is { } skipped)
                {
                    var odd = Encoding.Latin1.GetString(body, pos, 4);
                    fields.Add(new RawField(tagName, odd, null, body.AsSpan(pos + 10, skipped - 10).ToArray(), null, null)
                        { Quirk = "illegal frame identifier, stepped over" });
                    pos += skipped;
                    continue;
                }

                problem ??= $"no frame identifier at body offset {pos}";
                break;
            }

            var id = Encoding.ASCII.GetString(body, pos, idLength);
            int frameSize;
            ushort frameFlags = 0;
            if (major == 2)
            {
                frameSize = (int)ReadU24BE(body, pos + 3);
            }
            else
            {
                frameSize = major == 4 && !plainSizes ? (int)ReadSyncsafe(body, pos + 4) : (int)ReadU32BE(body, pos + 4);
                frameFlags = (ushort)ReadU16BE(body, pos + 8);
            }

            if (frameSize < 0 || pos + headerLength + frameSize > body.Length)
            {
                problem ??= $"frame {id} at body offset {pos} runs past the end of the tag";
                break;
            }

            var stored = body.AsSpan(pos + headerLength, frameSize).ToArray();
            fields.Add(DecodeFrame(tagName, major, id, frameFlags, stored, tagUnsync));
            pos += headerLength + frameSize;
        }

        if (plainSizes)
        {
            problem ??= "v2.4 frame sizes written as plain integers (read as such)";
        }

        return new RawTag(tagName, at, 10 + size + footer, fields, problem);
    }

    /// <summary>
    /// The length of the frame at <paramref name="pos"/>, header included, if its identifier is
    /// printable and its size lands on another frame, padding or the end of the tag; else null.
    /// </summary>
    static int? StepsCleanly(byte[] body, int pos, bool syncsafe)
    {
        if (pos + 10 > body.Length || body.AsSpan(pos, 4).ContainsAnyExceptInRange((byte)0x20, (byte)0x7E))
        {
            return null;
        }

        long size = syncsafe ? ReadSyncsafe(body, pos + 4) : ReadU32BE(body, pos + 4);
        var next = pos + 10 + size;
        return next == body.Length || next < body.Length && (body[next] == 0 || IsFrameId(body, (int)next, 4))
            ? (int)(10 + size)
            : null;
    }

    static bool WalksCleanly(byte[] body, int start, bool syncsafe)
    {
        // A long, so that a size near 2^32 cannot wrap the position round to somewhere valid.
        long pos = start;
        var frames = 0;
        while (pos + 10 <= body.Length)
        {
            if (body[pos] == 0)
            {
                return frames > 0;
            }

            if (!IsFrameId(body, (int)pos, 4))
            {
                return false;
            }

            long size = syncsafe ? ReadSyncsafe(body, (int)pos + 4) : ReadU32BE(body, (int)pos + 4);
            pos += 10 + size;
            frames++;
        }

        return pos == body.Length;
    }

    static RawField DecodeFrame(string tag, byte major, string id, ushort flags, byte[] stored, bool tagUnsync)
    {
        var data = stored;
        string? problem = null;
        bool compressed, encrypted, unsync = false, dataLength = false, grouped;
        if (major == 3)
        {
            compressed = (flags & 0x0080) != 0;
            encrypted = (flags & 0x0040) != 0;
            grouped = (flags & 0x0020) != 0;
        }
        else if (major == 4)
        {
            grouped = (flags & 0x0040) != 0;
            compressed = (flags & 0x0008) != 0;
            encrypted = (flags & 0x0004) != 0;
            unsync = (flags & 0x0002) != 0 || tagUnsync;
            dataLength = (flags & 0x0001) != 0;
        }
        else
        {
            compressed = encrypted = grouped = false;
        }

        try
        {
            var skip = 0;
            if (major == 3)
            {
                skip += compressed ? 4 : 0;
                skip += encrypted ? 1 : 0;
                skip += grouped ? 1 : 0;
            }
            else if (major == 4)
            {
                skip += grouped ? 1 : 0;
                skip += encrypted ? 1 : 0;
                skip += dataLength ? 4 : 0;
            }

            data = data[skip..];
            if (unsync)
            {
                data = Resynchronise(data);
            }

            if (encrypted)
            {
                return new RawField(tag, id, null, stored, null, null, "encrypted");
            }

            if (compressed)
            {
                using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
                using var o = new MemoryStream();
                z.CopyTo(o);
                data = o.ToArray();
            }
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return new RawField(tag, id, null, stored, null, null, $"cannot undo frame encoding: {e.GetType().Name}");
        }

        var (description, values, textProblem, quirk) = Text(id, data, major);
        return new RawField(tag, id, description, stored, data, values, problem ?? textProblem) { Quirk = quirk };
    }

    /// <summary>The description and values of a frame that holds text; nulls for any other frame.</summary>
    /// <summary>How a frame's UTF-16 text was read: the byte order its strings share, and what had to be assumed.</summary>
    sealed class Utf16State
    {
        public bool? BigEndian;
        public bool MarkMissing;
        public bool MarkInherited;
    }

    static (string? Description, string[]? Values, string? Problem, string? Quirk) Text(string id, byte[] c, byte major)
    {
        var state = new Utf16State();
        var (description, values, problem) = TextFields(id, c, state);
        var quirks = new List<string>();
        if (state.MarkMissing)
        {
            quirks.Add("UTF-16 without a byte order mark, read as little-endian");
        }

        if (state.MarkInherited)
        {
            quirks.Add("a UTF-16 value without its own byte order mark, read in the frame's byte order");
        }

        if (major < 4 && values is { Length: > 1 })
        {
            quirks.Add($"several values in a v2.{major} text frame");
        }

        if (id is "GRP1" or "MVNM" or "MVIN")
        {
            quirks.Add("Apple's text frame");
        }

        return (description, values, problem, quirks.Count == 0 ? null : string.Join("; ", quirks));
    }

    static (string? Description, string[]? Values, string? Problem) TextFields(string id, byte[] c, Utf16State state)
    {
        // Apple's GRP1, MVNM and MVIN are laid out as text frames (docs/design/quirks.md).
        var isPlainText = id[0] == 'T' && id is not ("TXXX" or "TXX") || id is "IPLS" or "GRP1" or "MVNM" or "MVIN";
        var isDescribed = id is "TXXX" or "TXX" or "WXXX" or "WXX" or "COMM" or "COM" or "USLT" or "ULT";
        var isUrl = id[0] == 'W' && !isDescribed;
        if (isUrl)
        {
            return (null, [Latin1.GetString(c)], null);
        }

        if (!isPlainText && !isDescribed)
        {
            return (null, null, null);
        }

        if (c.Length == 0)
        {
            return (null, null, "empty frame: no encoding byte");
        }

        var enc = c[0];
        if (enc > 3)
        {
            return (null, null, $"unknown text encoding {enc}");
        }

        var rest = c.AsSpan(1);
        string? description = null;
        if (isDescribed)
        {
            var hasLanguage = id is "COMM" or "COM" or "USLT" or "ULT";
            if (hasLanguage)
            {
                if (rest.Length < 3)
                {
                    return (null, null, "frame too short for its language");
                }

                rest = rest[3..];
            }

            var nul = FindTerminator(rest, enc);
            if (nul < 0)
            {
                return (null, null, "description has no terminator");
            }

            var d = Decode(enc, rest[..nul], state);
            if (d is null)
            {
                return (null, null, "description does not decode");
            }

            description = d;
            rest = rest[(nul + (enc is 1 or 2 ? 2 : 1))..];
            if (id is "WXXX" or "WXX")
            {
                return (description, [Latin1.GetString(rest)], null);
            }
        }

        var values = SplitValues(enc, rest, state, out var problem);
        return (description, values, problem);
    }

    static int FindTerminator(ReadOnlySpan<byte> b, byte enc)
    {
        if (enc is 1 or 2)
        {
            for (var i = 0; i + 1 < b.Length; i += 2)
            {
                if (b[i] == 0 && b[i + 1] == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        return b.IndexOf((byte)0);
    }

    static string[]? SplitValues(byte enc, ReadOnlySpan<byte> b, Utf16State state, out string? problem)
    {
        problem = null;
        var values = new List<string>();
        while (true)
        {
            var nul = FindTerminator(b, enc);
            var part = nul < 0 ? b : b[..nul];
            var s = Decode(enc, part, state);
            if (s is null)
            {
                problem = $"text does not decode in encoding {enc}";
                return null;
            }

            values.Add(s);
            if (nul < 0)
            {
                break;
            }

            b = b[(nul + (enc is 1 or 2 ? 2 : 1))..];
            if (b.Length == 0)
            {
                break; // a terminator after the last value
            }
        }

        return values.ToArray();
    }

    /// <summary>
    /// Decodes one string of a frame. In encoding 1 a string without a byte order mark takes the
    /// byte order of an earlier string in the frame, or little-endian if it is the first
    /// (implementation.md, and docs/design/quirks.md).
    /// </summary>
    static string? Decode(byte enc, ReadOnlySpan<byte> b, Utf16State state)
    {
        if (enc != 1 || b.Length < 2 || b[0] == 0xFF && b[1] == 0xFE || b[0] == 0xFE && b[1] == 0xFF)
        {
            if (enc == 1 && b.Length >= 2)
            {
                state.BigEndian = b[0] == 0xFE;
            }

            return DecodeStrict(enc, b);
        }

        if (b.Length % 2 != 0)
        {
            return null;
        }

        if (state.BigEndian is null)
        {
            state.MarkMissing = true;
            state.BigEndian = false;
        }
        else
        {
            state.MarkInherited = true;
        }

        try
        {
            return new UnicodeEncoding(state.BigEndian.Value, false, true).GetString(b);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>Decodes text in an Id3v2 encoding, or null where the bytes are not valid in it.</summary>
    public static string? DecodeStrict(byte enc, ReadOnlySpan<byte> b)
    {
        try
        {
            switch (enc)
            {
                case 0:
                    return Latin1.GetString(b);
                case 1:
                    if (b.Length == 0)
                    {
                        return "";
                    }

                    if (b.Length % 2 != 0 || b.Length < 2)
                    {
                        return null;
                    }

                    if (b[0] == 0xFF && b[1] == 0xFE)
                    {
                        return new UnicodeEncoding(false, false, true).GetString(b[2..]);
                    }

                    if (b[0] == 0xFE && b[1] == 0xFF)
                    {
                        return new UnicodeEncoding(true, false, true).GetString(b[2..]);
                    }

                    return null; // no byte order mark
                case 2:
                    return b.Length % 2 != 0 ? null : new UnicodeEncoding(true, false, true).GetString(b);
                default:
                    return StrictUtf8.GetString(b);
            }
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    // ============================================================================ APE

    /// <summary>Reads the APE tag whose footer ends at <paramref name="end"/>; null if there is none.</summary>
    public static RawTag? ApeFromFooter(byte[] f, long end)
    {
        var footer = (int)end - 32;
        if (footer < 0 || !StartsWith(f, footer, "APETAGEX"))
        {
            return null;
        }

        var version = ReadU32LE(f, footer + 8);
        var size = ReadU32LE(f, footer + 12);
        var count = ReadU32LE(f, footer + 16);
        var flags = ReadU32LE(f, footer + 20);
        var hasHeader = (flags & 0x80000000) != 0;
        var tag = version >= 2000 ? "ape" : "ape1";
        if (size < 32 || size > footer + 32)
        {
            return new RawTag(tag, footer, 32, [], $"tag size {size} runs past the start of the file");
        }

        var itemsStart = footer + 32 - (int)size;
        var start = hasHeader ? itemsStart - 32 : itemsStart;
        var tagResult = Items(f, tag, version, itemsStart, footer, count);
        return tagResult with { Offset = start, Length = end - start };
    }

    public static RawTag? ApeFromHeader(byte[] f, int at)
    {
        if (!StartsWith(f, at, "APETAGEX") || at + 32 > f.Length)
        {
            return null;
        }

        var flags = ReadU32LE(f, at + 20);
        if ((flags & 0x20000000) == 0)
        {
            return null; // a footer, not a header
        }

        var size = ReadU32LE(f, at + 12);
        var footerEnd = at + 32 + (int)size;
        return footerEnd <= f.Length ? ApeFromFooter(f, footerEnd) : new RawTag("ape", at, 32, [], "tag size runs past the end of the file");
    }

    static RawTag Items(byte[] f, string tag, uint version, int start, int end, uint count)
    {
        var fields = new List<RawField>();
        string? problem = null;
        var pos = start;
        for (var i = 0; i < count; i++)
        {
            if (pos + 8 > end)
            {
                problem = $"item count {count} but only {i} items fit";
                break;
            }

            var valueSize = ReadU32LE(f, pos);
            var itemFlags = ReadU32LE(f, pos + 4);
            var keyEnd = Array.IndexOf(f, (byte)0, pos + 8, end - pos - 8);
            if (keyEnd < 0)
            {
                problem = $"item {i} has no key terminator";
                break;
            }

            var key = Encoding.ASCII.GetString(f, pos + 8, keyEnd - pos - 8);
            if (keyEnd + 1 + valueSize > end)
            {
                problem = $"item {key} runs past the end of the tag";
                break;
            }

            var value = f.AsSpan(keyEnd + 1, (int)valueSize).ToArray();
            var kind = (itemFlags >> 1) & 3;
            string[]? values = null;
            string? itemProblem = null;
            if (kind != 1)
            {
                var s = version >= 2000 ? DecodeStrict(3, value) : Latin1.GetString(value);
                if (s is null)
                {
                    itemProblem = "text item is not valid UTF-8";
                }
                else
                {
                    values = s.Split('\0');
                }
            }

            fields.Add(new RawField(tag, key, null, value, value, values, itemProblem));
            pos = keyEnd + 1 + (int)valueSize;
        }

        return new RawTag(tag, start, end + 32 - start, fields, problem);
    }

    // ============================================================================ Id3v1

    public static RawTag? Id3v1(byte[] f)
    {
        var at = f.Length - 128;
        if (at < 0 || !StartsWith(f, at, "TAG"))
        {
            return null;
        }

        string Field(int offset, int length)
        {
            var span = f.AsSpan(at + offset, length);
            var nul = span.IndexOf((byte)0);
            return Latin1.GetString(nul < 0 ? span : span[..nul]).TrimEnd(' ');
        }

        var v11 = f[at + 125] == 0 && f[at + 126] != 0;
        var fields = new List<RawField>
        {
            new("id3v1", "title", null, f[(at + 3)..(at + 33)], null, [Field(3, 30)]),
            new("id3v1", "artist", null, f[(at + 33)..(at + 63)], null, [Field(33, 30)]),
            new("id3v1", "album", null, f[(at + 63)..(at + 93)], null, [Field(63, 30)]),
            new("id3v1", "year", null, f[(at + 93)..(at + 97)], null, [Field(93, 4)]),
            new("id3v1", "comment", null, f[(at + 97)..(at + (v11 ? 125 : 127))], null, [Field(97, v11 ? 28 : 30)]),
            new("id3v1", "track", null, v11 ? [f[at + 126]] : [], null, v11 ? [f[at + 126].ToString()] : null),
            new("id3v1", "genre", null, [f[at + 127]], null, [f[at + 127].ToString()]),
        };
        return new RawTag(v11 ? "id3v1.1" : "id3v1.0", at, 128, fields);
    }

    // ============================================================================ Vorbis comments

    /// <summary>Reads a comment structure: vendor, count, then length-prefixed KEY=value comments.</summary>
    public static RawTag VorbisComment(byte[] data, int start, long fileOffset, string tag = "vorbis")
    {
        var fields = new List<RawField>();
        if (start + 4 > data.Length)
        {
            return new RawTag(tag, fileOffset, data.Length, fields, "no vendor length");
        }

        var vendorLength = ReadU32LE(data, start);
        var pos = start + 4L + vendorLength;
        if (pos + 4 > data.Length)
        {
            return new RawTag(tag, fileOffset, data.Length, fields, $"vendor length {vendorLength} runs past the end");
        }

        var count = ReadU32LE(data, (int)pos);
        pos += 4;
        for (var i = 0; i < count; i++)
        {
            if (pos + 4 > data.Length)
            {
                return new RawTag(tag, fileOffset, data.Length, fields, $"comment count {count} but only {i} comments fit");
            }

            var length = ReadU32LE(data, (int)pos);
            pos += 4;
            if (pos + length > data.Length)
            {
                return new RawTag(tag, fileOffset, data.Length, fields, $"comment {i} length {length} runs past the end");
            }

            var c = data.AsSpan((int)pos, (int)length).ToArray();
            pos += length;
            var eq = Array.IndexOf(c, (byte)'=');
            if (eq < 0)
            {
                fields.Add(new RawField(tag, Latin1.GetString(c), null, c, null, null, "no '=' separator"));
                continue;
            }

            var key = Latin1.GetString(c, 0, eq);
            var value = c[(eq + 1)..];
            var keyOk = c[..eq].All(b => b is >= 0x20 and <= 0x7D and not (byte)'=');
            var text = DecodeStrict(3, value);
            fields.Add(new RawField(tag, key, null, value, value, text is null ? null : [text],
                text is null ? "value is not valid UTF-8" : keyOk ? null : "key holds a character the spec forbids"));
        }

        return new RawTag(tag, fileOffset, pos - start, fields);
    }
}
