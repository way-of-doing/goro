using System.Text;
using System.Text.Json;
using static Goro.Scratchpad.FileReading.Bin;
using static Goro.Scratchpad.FileReading.TagWriters;

namespace Goro.Scratchpad.FileReading;

/// <summary>One datum a fixture records, as the builder wrote it: the reference a reader is judged against.</summary>
public sealed record Recorded(string Tag, string Key, string? Description, string[]? Values, string BytesHex, string? Note = null);

/// <summary>
/// A file of the corpus. <see cref="Group"/> says what it is for: <c>layout</c> files are healthy and
/// differ in where tags sit and how they are written; <c>tag-damage</c> files have a damaged tag over
/// healthy audio; <c>audio-damage</c> files have damaged or unusual audio and healthy (or no) tags.
/// </summary>
public sealed record Fixture(string Name, string Group, string Description, Func<byte[]> Build)
{
    public List<Recorded> Recorded { get; } = [];
}

/// <summary>
/// Builds the audio fixture corpus from the encoder-made seeds (make-seeds.sh). Every byte of a tag
/// and every piece of damage is written here, so the manifest can say exactly what each file holds.
/// </summary>
public static class Corpus
{
    static string seeds = "";

    static byte[] Seed(string name) => File.ReadAllBytes(Path.Combine(seeds, name));

    public static List<Fixture> Define(string seedDir)
    {
        seeds = seedDir;
        var all = new List<Fixture>();
        all.AddRange(Mp3Layouts());
        all.AddRange(Mp3TagDamage());
        all.AddRange(Mp3AudioDamage());
        all.AddRange(OggFixtures());
        all.AddRange(FlacFixtures());
        return all;
    }

    public static void Write(string corpusDir, string seedDir)
    {
        var fixtures = Define(seedDir);
        foreach (var f in fixtures)
        {
            var path = Path.Combine(corpusDir, f.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, f.Build());
        }

        var manifest = fixtures.Select(f => new { file = f.Name, group = f.Group, description = f.Description, recorded = f.Recorded });
        File.WriteAllText(Path.Combine(corpusDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
        File.WriteAllText(Path.Combine(corpusDir, "README.md"), Readme(fixtures));
        Console.WriteLine($"{fixtures.Count} fixtures written to {corpusDir}");
    }

    static string Readme(List<Fixture> fixtures)
    {
        var sb = new StringBuilder("""
            # Audio fixture corpus

            Small audio files, healthy and damaged, for testing anything that reads tags, finds the audio
            or works out a playing time. Every file is 3.3 seconds of audio or less, mono where the format
            allows.

            - `seeds/` holds the only files an encoder made, by
              `scratchpad/Goro.Scratchpad/FileReading/make-seeds.sh`. They carry no tags beyond what the
              encoder writes (LAME's Info frame, the encoder's vendor comment).
            - Every other file is derived from a seed, byte for byte, by
              `scratchpad/Goro.Scratchpad/FileReading/Corpus.cs`: tags are written by hand rather than
              through a tag library, and damage is applied at known offsets. Rebuilding with
              `dotnet run -c Release --project scratchpad/Goro.Scratchpad -- corpus` gives identical bytes.
            - `manifest.json` lists every derived file with what it records: for each tag field, the tag,
              the key as written, the description where the frame has one, the values as text, and the
              content bytes (as hex, or a SHA-256 for values over 4 KiB). It is the reference a reader is
              judged against.

            Groups: **layout** files are healthy and differ in where the tags sit and how they are written;
            **tag-damage** files hold a damaged tag over healthy audio; **audio-damage** files hold damaged or
            unusual audio. Names starting `dmg-` are tag damage.

            The seeds' playing times, as ffmpeg 9.0.2 decodes them: 3.300 s for every seed made from the main
            source with gapless information (LAME tag, Opus pre-skip, Vorbis and FLAC exact lengths), 3.344 s
            for the MPEG-1 seeds without an Info frame (128 frames of 1152 samples), 3.456 s for the MPEG-2.5
            seed, 1.700 s for `mp3-cbr-b.mp3`, and 5.010 s for `ogg-chained-vorbis.ogg`. Two seeds are beyond
            ffmpeg: it cannot read `mp3-freeformat.mp3`, which LAME decodes to 3.300 s, and it decodes only
            the first link of `ogg-chained-vorbis-opus.ogg`, whose links are 3.300 s and 1.700 s long.

            """.Replace("            ", ""));
        foreach (var format in fixtures.GroupBy(f => f.Name.Split('/')[0]))
        {
            sb.Append($"\n## {format.Key}\n");
            foreach (var group in format.GroupBy(f => f.Group))
            {
                sb.Append($"\n### {group.Key}\n\n| File | What it is |\n|------|------------|\n");
                foreach (var f in group)
                {
                    sb.Append($"| `{f.Name.Split('/')[1]}` | {f.Description.Replace("|", "\\|")} |\n");
                }
            }
        }

        return sb.ToString();
    }

    // ================================================================================ MP3

    static byte[] Mp3(byte[] audio, byte[][] before, byte[][] after) => Concat(Concat(before), audio, Concat(after));

    static Fixture Add(this List<Fixture> list, string name, string group, string description, Func<byte[]> build)
    {
        var f = new Fixture(name, group, description, build);
        list.Add(f);
        return f;
    }

    static Fixture Records(this Fixture f, string tag, IEnumerable<Frame> frames)
    {
        foreach (var fr in frames)
        {
            f.Recorded.Add(new Recorded(tag, fr.Id, DescriptionOf(fr), TextOf(fr), Fingerprint(fr.Content)));
        }

        return f;
    }

    static Fixture Records(this Fixture f, IEnumerable<ApeItem> items, bool version1 = false)
    {
        foreach (var i in items)
        {
            // APEv1 specified ISO-8859-1 text; v2 specifies UTF-8.
            var values = (i.Flags & 6) == 2 ? null : (version1 ? Latin1 : Utf8).GetString(i.Value).Split('\0');
            f.Recorded.Add(new Recorded(version1 ? "ape1" : "ape", i.Key, null, values, Fingerprint(i.Value)));
        }

        return f;
    }

    static Fixture Comments(this Fixture f, string tag, IEnumerable<byte[]> comments)
    {
        foreach (var c in comments)
        {
            var eq = Array.IndexOf(c, (byte)'=');
            var key = eq < 0 ? Latin1.GetString(c) : Latin1.GetString(c, 0, eq);
            var value = eq < 0 ? Array.Empty<byte>() : c[(eq + 1)..];
            string? text;
            try
            {
                text = StrictUtf8.GetString(value);
            }
            catch (DecoderFallbackException)
            {
                text = null;
            }

            f.Recorded.Add(new Recorded(tag, key, null, text is null ? null : [text], Fingerprint(value),
                eq < 0 ? "no '=' separator" : text is null ? "value is not valid UTF-8" : null));
        }

        return f;
    }

    /// <summary>The bytes as hex, or a SHA-256 for values too large to be worth listing.</summary>
    public static string Fingerprint(byte[] b) =>
        b.Length <= 4096 ? Convert.ToHexString(b).ToLowerInvariant() : "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b)).ToLowerInvariant();

    static Fixture Note(this Fixture f, string tag, string key, string note)
    {
        f.Recorded.Add(new Recorded(tag, key, null, null, "", note));
        return f;
    }

    static string? DescriptionOf(Frame f)
    {
        if (f.Id is not ("TXXX" or "WXXX" or "COMM" or "USLT" or "TXX" or "COM"))
        {
            return null;
        }

        var c = f.Content;
        var enc = c[0];
        var start = f.Id is "COMM" or "USLT" or "COM" ? 4 : 1;
        var end = FindNul(c, start, enc);
        return Decode(enc, c.AsSpan(start, end - start));
    }

    static string[]? TextOf(Frame f)
    {
        var c = f.Content;
        if (c.Length == 0)
        {
            return [];
        }

        var isText = f.Id[0] == 'T' && f.Id is not ("TXXX" or "TXX");
        if (isText)
        {
            var enc = c[0];
            var s = Decode(enc, c.AsSpan(1));
            if (s.EndsWith('\0'))
            {
                s = s[..^1];
            }

            return s.Split('\0');
        }

        if (f.Id is "TXXX" or "WXXX" or "COMM" or "USLT" or "TXX" or "COM")
        {
            var enc = c[0];
            var start = f.Id is "COMM" or "USLT" or "COM" ? 4 : 1;
            var end = FindNul(c, start, enc);
            var valueStart = end + (enc is 1 or 2 ? 2 : 1);
            var valueEncoding = f.Id is "WXXX" ? (byte)0 : enc;
            return [Decode(valueEncoding, c.AsSpan(Math.Min(valueStart, c.Length)))];
        }

        if (f.Id[0] == 'W')
        {
            return [Latin1.GetString(c)];
        }

        return null;
    }

    static int FindNul(byte[] c, int start, byte enc)
    {
        if (enc is 1 or 2)
        {
            for (var i = start; i + 1 < c.Length; i += 2)
            {
                if (c[i] == 0 && c[i + 1] == 0)
                {
                    return i;
                }
            }

            return c.Length;
        }

        var at = Array.IndexOf(c, (byte)0, start);
        return at < 0 ? c.Length : at;
    }

    static string Decode(byte enc, ReadOnlySpan<byte> b) => enc switch
    {
        0 => Latin1.GetString(b),
        1 => b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE ? Encoding.Unicode.GetString(b[2..])
            : b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF ? Encoding.BigEndianUnicode.GetString(b[2..])
            : Encoding.Unicode.GetString(b),
        2 => Encoding.BigEndianUnicode.GetString(b),
        _ => Utf8.GetString(b),
    };

    // The standard v2.4 tag: one of nearly everything a predicate can ask about.
    static List<Frame> V24Frames() =>
    [
        new("TIT2", Text(3, "Title ✓ – Ünïcødé")),
        new("TPE1", Text(3, "AC/DC", "Motörhead")),
        new("TALB", Text(1, "Album (UTF-16)")),
        new("TCON", Text(0, "(17)Post-Rock")),
        new("TRCK", Text(0, "3/12")),
        new("TDRC", Text(0, "1991-01-02")),
        new("TIT1", Text(3, "  padded  ")),
        new("TPE2", Text(3, "")),
        new("TZZZ", Text(3, "a text frame no library knows")),
        new("TXXX", Described(3, "MOOD", "calm")),
        new("TXXX", Described(3, "mood", "wistful")),
        new("COMM", Comment(3, "eng", "", "A comment")),
        new("COMM", Comment(0, "eng", "iTunNORM", " 00000A2F 0000096E")),
        new("USLT", Comment(3, "eng", "", "la la la")),
        new("WOAR", Url("https://example.com/artist")),
        new("WXXX", Concat([0], Latin1.GetBytes("homepage"), [0], Latin1.GetBytes("https://example.com"))),
        new("APIC", Apic("image/png", 3, "", Png)),
        new("PRIV", Priv("com.example", [1, 2, 3, 0xFF, 0xE0])),
        new("POPM", Popm("rater@example.com", 196, 5)),
    ];

    static List<Frame> V23Frames() =>
    [
        new("TIT2", Text(1, "Title (UTF-16) ✓")),
        new("TPE1", Text(0, "AC/DC")),
        new("TALB", TextTerminated(0, "Album, NUL-terminated")),
        new("TCON", Text(0, "Rock/Metal")),
        new("TRCK", Text(0, "3/12")),
        new("TYER", Text(0, "1991")),
        new("TDAT", Text(0, "0201")),
        new("TIME", Text(0, "1200")),
        new("TORY", Text(0, "1990")),
        new("IPLS", Text(0, "producer", "Someone")),
        new("RVAD", [0x03, 0x10, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00]),
        new("TXXX", Described(1, "MOOD", "calm")),
        new("COMM", Comment(0, "eng", "", "A comment")),
        new("APIC", Apic("image/png", 3, "", Png)),
    ];

    static List<Frame> V22Frames() =>
    [
        new("TT2", Text(0, "Title v2.2")),
        new("TP1", Text(1, "Artist (UTF-16)")),
        new("TAL", Text(0, "Album")),
        new("TCO", Text(0, "(17)")),
        new("TRK", Text(0, "3/12")),
        new("TYE", Text(0, "1991")),
        new("TXX", Described(0, "MOOD", "calm")),
        new("COM", Comment(0, "eng", "", "A comment")),
        new("PIC", Pic("PNG", 3, "", Png)),
    ];

    static List<ApeItem> ApeItems() =>
    [
        ApeItem.TextItem("Title", "Title ✓"),
        ApeItem.TextItem("Artist", "AC/DC", "Motörhead"),
        ApeItem.TextItem("Album", "Album"),
        ApeItem.TextItem("Year", "1991-01-02"),
        ApeItem.TextItem("Track", "3/12"),
        ApeItem.TextItem("Genre", "17"),
        ApeItem.TextItem("Album Artist", "Various"),
        ApeItem.Binary("Cover Art (Front)", Concat(Ascii("cover.png"), [0], Png)),
        ApeItem.Locator("Related", "https://example.com/related"),
    ];

    static byte[] StandardId3v1(int? track = 3) => Id3v1("Title v1", "Artist v1", "Album v1", "1991", "Comment v1", track, 17);

    static IEnumerable<Fixture> Mp3Layouts()
    {
        var l = new List<Fixture>();
        var cbr = () => Seed("mp3-cbr.mp3");
        const string g = "layout";

        l.Add("mp3/id3v24.mp3", g, "Id3v2.4 tag holding the standard frame set: UTF-8, UTF-16 and Latin-1 text, a two-value TPE1, TXXX twice with descriptions differing in case, two COMM, USLT, URL frames, APIC, PRIV, POPM, an empty TPE2, an unknown text frame TZZZ.",
                () => Mp3(cbr(), [Id3v2(4, V24Frames(), new() { Padding = 512 })], []))
            .Records("id3v2.4", V24Frames());
        l.Add("mp3/id3v24-utf16be.mp3", g, "Id3v2.4 text in encoding 2, UTF-16BE without a byte order mark.",
                () => Mp3(cbr(), [Id3v2(4, [new("TIT2", Text(2, "Big-endian ✓")), new("TPE1", Text(2, "A", "B"))])], []))
            .Records("id3v2.4", [new("TIT2", Text(2, "Big-endian ✓")), new("TPE1", Text(2, "A", "B"))]);
        l.Add("mp3/id3v23.mp3", g, "Id3v2.3 tag: UTF-16 with BOM, a NUL-terminated value, TCON with a slash, TYER/TDAT/TIME, TORY, IPLS, RVAD.",
                () => Mp3(cbr(), [Id3v2(3, V23Frames(), new() { Padding = 256 })], []))
            .Records("id3v2.3", V23Frames());
        l.Add("mp3/id3v22.mp3", g, "Id3v2.2 tag: three-letter frames, TCO as a bare reference, PIC with a three-letter image format.",
                () => Mp3(cbr(), [Id3v2(2, V22Frames())], []))
            .Records("id3v2.2", V22Frames());

        var unsyncFrames = new List<Frame> { new("TIT2", Text(0, "Unsynchronised tag")), new("APIC", Apic("image/png", 3, "", SyncBait)) };
        l.Add("mp3/id3v23-unsync.mp3", g, "Id3v2.3 with the unsynchronisation flag: the whole tag body is unsynchronised, so the APIC holding 0xFF 0xE0 sequences is stored with inserted zero bytes.",
                () => Mp3(cbr(), [Id3v2(3, unsyncFrames, new() { Unsync = true })], []))
            .Records("id3v2.3", unsyncFrames);
        l.Add("mp3/id3v24-frame-unsync.mp3", g, "Id3v2.4 with one frame unsynchronised (frame flag n) and a data length indicator (flag p).",
                () => Mp3(cbr(), [Id3v2(4, [new("TIT2", Text(0, "Frame-level unsync")), new("APIC", Apic("image/png", 3, "", SyncBait)) { Unsync = true, DataLength = true }])], []))
            .Records("id3v2.4", unsyncFrames.Select(f => f with { Content = f.Id == "TIT2" ? Text(0, "Frame-level unsync") : f.Content }));
        l.Add("mp3/id3v24-tag-unsync.mp3", g, "Id3v2.4 with the tag-level unsynchronisation flag, every frame unsynchronised and flagged n.",
                () => Mp3(cbr(), [Id3v2(4, unsyncFrames, new() { Unsync = true })], []))
            .Records("id3v2.4", unsyncFrames);

        var compressed = new List<Frame> { new("TIT2", Text(0, "Compressed frames")), new("COMM", Comment(0, "eng", "", new string('z', 400))) { Compress = true } };
        l.Add("mp3/id3v23-compressed.mp3", g, "Id3v2.3 with a zlib-compressed COMM frame (flag i, decompressed size prefixed).",
                () => Mp3(cbr(), [Id3v2(3, compressed)], []))
            .Records("id3v2.3", compressed);
        l.Add("mp3/id3v24-compressed.mp3", g, "Id3v2.4 with a zlib-compressed COMM frame (flags k and p).",
                () => Mp3(cbr(), [Id3v2(4, compressed)], []))
            .Records("id3v2.4", compressed);
        var encrypted = new List<Frame>
        {
            new("TIT2", Text(0, "Encrypted frame")),
            new("ENCR", Concat(Latin1.GetBytes("owner@example.com"), [0, 0x80])),
            new("TPE1", Text(0, "secret artist")) { EncryptionMethod = 0x80 },
        };
        l.Add("mp3/id3v23-encrypted.mp3", g, "Id3v2.3 with a TPE1 flagged as encrypted (method 0x80, registered by ENCR). The content is plain text; no reader can know that.",
                () => Mp3(cbr(), [Id3v2(3, encrypted)], []))
            .Records("id3v2.3", encrypted);
        l.Add("mp3/id3v23-exthdr.mp3", g, "Id3v2.3 with an extended header.",
                () => Mp3(cbr(), [Id3v2(3, V23Frames().Take(3), new() { ExtendedHeader = true, Padding = 64 })], []))
            .Records("id3v2.3", V23Frames().Take(3));
        l.Add("mp3/id3v24-exthdr-footer.mp3", g, "Id3v2.4 with an extended header and a footer.",
                () => Mp3(cbr(), [Id3v2(4, V24Frames().Take(3), new() { ExtendedHeader = true, Footer = true })], []))
            .Records("id3v2.4", V24Frames().Take(3));
        l.Add("mp3/id3v24-big-padding.mp3", g, "Id3v2.4 with 64 KiB of padding, as taggers leave to make room for later edits.",
                () => Mp3(cbr(), [Id3v2(4, V24Frames().Take(2), new() { Padding = 65536 })], []))
            .Records("id3v2.4", V24Frames().Take(2));
        var dup = new List<Frame> { new("TIT2", Text(0, "first title")), new("TIT2", Text(0, "second title")), new("TPE1", Text(0, "artist")) };
        l.Add("mp3/id3v24-duplicate-text-frames.mp3", g, "Id3v2.4 with TIT2 written twice, which the format forbids but taggers produce.",
                () => Mp3(cbr(), [Id3v2(4, dup)], []))
            .Records("id3v2.4", dup);
        l.Add("mp3/id3v2-twice.mp3", g, "Two Id3v2 tags back to back (v2.4 then v2.3), as some taggers leave behind.",
                () => Mp3(cbr(), [Id3v2(4, [new("TIT2", Text(0, "first tag"))]), Id3v2(3, [new("TIT2", Text(0, "second tag"))])], []))
            .Records("id3v2.4", [new("TIT2", Text(0, "first tag"))])
            .Records("id3v2.3", [new("TIT2", Text(0, "second tag"))]);
        l.Add("mp3/id3v24-appended.mp3", g, "An Id3v2.4 tag with a footer appended after the audio (and before an Id3v1 tag), as the format allows.",
                () => Mp3(cbr(), [], [Id3v2(4, [new("TIT2", Text(0, "appended tag"))], new() { Footer = true }), StandardId3v1()]))
            .Records("id3v2.4", [new("TIT2", Text(0, "appended tag"))]);
        l.Add("mp3/id3v10.mp3", g, "Id3v1.0: a 30-byte comment, no track number.",
            () => Mp3(cbr(), [], [Id3v1("Title v1", "Artist v1", "Album v1", "1991", "A thirty byte comment, exactly", null, 17)])).Note("id3v1", "*", "v1.0, genre 17 (Rock)");
        l.Add("mp3/id3v11.mp3", g, "Id3v1.1: track 3 in the last comment byte, genre 17.",
            () => Mp3(cbr(), [], [StandardId3v1()])).Note("id3v1", "*", "v1.1, track 3, genre 17 (Rock)");
        l.Add("mp3/id3v11-latin1-genre255.mp3", g, "Id3v1.1 with Latin-1 text outside ASCII and genre byte 255 (none).",
            () => Mp3(cbr(), [], [Id3v1("Motörhead", "Björk", "Album", "1991", "", 1, 255)])).Note("id3v1", "*", "Latin-1 text, genre 255");
        l.Add("mp3/ape2.mp3", g, "APEv2 tag with header and footer: a two-value Artist, a binary cover item, a locator item.",
                () => Mp3(cbr(), [], [Ape(ApeItems())]))
            .Records(ApeItems());
        l.Add("mp3/ape2-footer-only.mp3", g, "APEv2 tag with a footer and no header.",
                () => Mp3(cbr(), [], [Ape(ApeItems(), new() { Header = false })]))
            .Records(ApeItems());
        var ape1 = new List<ApeItem> { new("Title", 0, Latin1.GetBytes("Motörhead")), ApeItem.TextItem("Artist", "Plain ASCII") };
        l.Add("mp3/ape1-latin1.mp3", g, "APEv1 tag (version 1000, footer only) whose Title is ISO-8859-1, as v1 specified.",
                () => Mp3(cbr(), [], [Ape(ape1, new() { Version = 1000 })]))
            .Records(ape1, version1: true);
        l.Add("mp3/ape2-id3v1.mp3", g, "APEv2 followed by Id3v1, the usual order.",
                () => Mp3(cbr(), [], [Ape(ApeItems()), StandardId3v1()]))
            .Records(ApeItems());
        l.Add("mp3/ape2-lyrics3-id3v1.mp3", g, "APEv2, then a Lyrics3v2 block, then Id3v1.",
                () => Mp3(cbr(), [], [Ape(ApeItems()), Lyrics3v2("la la la"), StandardId3v1()]))
            .Records(ApeItems());
        l.Add("mp3/ape2-at-start.mp3", g, "APEv2 tag (with header) before the audio, which some tools write and the format does not forbid.",
                () => Mp3(cbr(), [Ape(ApeItems())], []))
            .Records(ApeItems());
        var apeCase = new List<ApeItem> { ApeItem.TextItem("ARTIST", "upper"), ApeItem.TextItem("Artist", "mixed") };
        l.Add("mp3/ape2-keys-differing-in-case.mp3", g, "APEv2 with ARTIST and Artist, which the format forbids.",
                () => Mp3(cbr(), [], [Ape(apeCase)]))
            .Records(apeCase);
        l.Add("mp3/all-tags.mp3", g, "Id3v2.4, then audio, then APEv2, then Id3v1: every tag an MP3 commonly carries.",
                () => Mp3(cbr(), [Id3v2(4, V24Frames())], [Ape(ApeItems()), StandardId3v1()]))
            .Records("id3v2.4", V24Frames()).Records(ApeItems());
        l.Add("mp3/id3v24-junk-before-audio.mp3", g, "Id3v2.4 followed by 1000 zero bytes the tag size does not cover, then the audio.",
                () => Mp3(cbr(), [Id3v2(4, V24Frames().Take(2)), new byte[1000]], []))
            .Records("id3v2.4", V24Frames().Take(2));
        return l;
    }

    static IEnumerable<Fixture> Mp3TagDamage()
    {
        var l = new List<Fixture>();
        var cbr = () => Seed("mp3-cbr.mp3");
        const string g = "tag-damage";
        var good = V24Frames();

        l.Add("mp3/dmg-id3v2-size-past-eof.mp3", g, "Id3v2.4 tag size field claims 10 MB, more than the file holds.",
            () => Mp3(cbr(), [Id3v2(4, good, new() { SizeOverride = 10_000_000 })], []));
        l.Add("mp3/dmg-id3v2-size-short.mp3", g, "Id3v2.4 tag size field 300 bytes too short: it ends inside a frame, and the rest of the tag sits where the audio should start.",
            () =>
            {
                var tag = Id3v2(4, good);
                var size = (uint)(tag.Length - 10 - 300);
                return Mp3(cbr(), [Id3v2(4, good, new() { SizeOverride = size })], []);
            });
        l.Add("mp3/dmg-id3v2-size-not-syncsafe.mp3", g, "Id3v2.4 tag size written as a plain integer (a byte with its top bit set), as some broken taggers do.",
            () => Mp3(cbr(), [Id3v2(4, good.Append(new("TXXX", Described(0, "filler", new string('f', 300)))), new() { PlainTagSize = true })], []));
        l.Add("mp3/dmg-id3v24-frame-overrun.mp3", g, "Id3v2.4: the fourth frame (TCON) declares a size that runs past the end of the tag.",
            () => Mp3(cbr(), [Id3v2(4, good.Select((f, i) => i == 3 ? f with { SizeOverride = 5000 } : f))], []));
        l.Add("mp3/dmg-id3v24-itunes-sizes.mp3", g, "Id3v2.4 with every frame size written as a plain integer, as iTunes once did; the 400-byte COMM makes the difference visible.",
            () => Mp3(cbr(), [Id3v2(4, good.Take(3).Append(new("COMM", Comment(0, "eng", "", new string('c', 400)))).Append(new("TRCK", Text(0, "7"))).Select(f => f with { PlainSize = true }))], []));
        l.Add("mp3/dmg-id3v24-bad-frame-id.mp3", g, "Id3v2.4: the third frame's identifier is 't!t2', not a legal identifier.",
            () => Mp3(cbr(), [Id3v2(4, good.Select((f, i) => i == 2 ? f with { Id = "t!t2" } : f))], []));
        l.Add("mp3/dmg-id3v24-zero-size-frame.mp3", g, "Id3v2.4: a TIT3 frame with size 0 in the middle of the tag.",
            () => Mp3(cbr(), [Id3v2(4, good.Take(2).Append(new("TIT3", [])).Concat(good.Skip(2)))], []));
        l.Add("mp3/dmg-id3v24-bad-encoding.mp3", g, "Id3v2.4: TIT2's encoding byte is 5, which no revision defines.",
            () => Mp3(cbr(), [Id3v2(4, good.Select(f => f.Id == "TIT2" ? f with { Content = Concat([5], Utf8.GetBytes("Title")) } : f))], []));
        l.Add("mp3/dmg-id3v24-utf16-no-bom.mp3", g, "Id3v2.4: TIT2 in encoding 1 (UTF-16) without a byte order mark.",
            () => Mp3(cbr(), [Id3v2(4, good.Select(f => f.Id == "TIT2" ? f with { Content = Concat([1], Encoding.Unicode.GetBytes("No BOM")) } : f))], []));
        l.Add("mp3/dmg-id3v24-utf16-odd-length.mp3", g, "Id3v2.4: TIT2 in UTF-16 with an odd number of bytes.",
            () => Mp3(cbr(), [Id3v2(4, good.Select(f => f.Id == "TIT2" ? f with { Content = Concat([1, 0xFF, 0xFE], Encoding.Unicode.GetBytes("Odd"), [0x41]) } : f))], []));
        l.Add("mp3/dmg-id3v24-invalid-utf8.mp3", g, "Id3v2.4: TPE1 declares UTF-8 and holds 'Mot\\xF6rhead' in Latin-1.",
            () => Mp3(cbr(), [Id3v2(4, good.Select(f => f.Id == "TPE1" ? f with { Content = Concat([3], Latin1.GetBytes("Motörhead")) } : f))], []));
        l.Add("mp3/dmg-id3v23-codepage-as-latin1.mp3", g, "Id3v2.3: TPE1 declares Latin-1 but holds the local codepage (Windows-1251 Cyrillic), the classic mis-tagged file.",
            () =>
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Mp3(cbr(), [Id3v2(3, [new("TPE1", Concat([0], Encoding.GetEncoding(1251).GetBytes("Кино"))), new("TIT2", Text(0, "Title"))])], []);
            });
        l.Add("mp3/dmg-id3v2-version-5.mp3", g, "A tag claiming Id3v2.5, a revision that does not exist, laid out as v2.4.",
            () => Mp3(cbr(), [Id3v2(4, good, new() { MajorOverride = 5 })], []));
        l.Add("mp3/dmg-id3v24-bad-zlib.mp3", g, "Id3v2.4: COMM flagged compressed whose data is not zlib.",
            () => Mp3(cbr(), [Id3v2(4, [new("TIT2", Text(0, "Title")), new("COMM", Comment(0, "eng", "", "not compressed")) { Flags = 0x0008 | 0x0001 }, new("TRCK", Text(0, "4"))])], []));
        l.Add("mp3/dmg-id3v24-truncated-in-tag.mp3", g, "A file that ends inside its Id3v2.4 tag: no audio at all.",
            () => Id3v2(4, good)[..200]);
        l.Add("mp3/dmg-id3v24-zeroed-block.mp3", g, "Id3v2.4 with 64 bytes in the middle of the frames zeroed, as bit rot would.",
            () =>
            {
                var tag = Id3v2(4, good);
                Array.Clear(tag, 120, 64);
                return Mp3(cbr(), [tag], []);
            });
        l.Add("mp3/dmg-id3v1-garbage.mp3", g, "'TAG' followed by 125 random bytes.",
            () => Mp3(cbr(), [], [Concat(Ascii("TAG"), RandomBytes(125, 7))]));
        l.Add("mp3/dmg-ape-item-count-high.mp3", g, "APEv2 footer claims 50 items; there are 9.",
            () => Mp3(cbr(), [], [Ape(ApeItems(), new() { ItemCountOverride = 50 }), StandardId3v1()]));
        l.Add("mp3/dmg-ape-size-past-start.mp3", g, "APEv2 footer claims a tag larger than the file.",
            () => Mp3(cbr(), [], [Ape(ApeItems(), new() { TagSizeOverride = 10_000_000, Header = false }), StandardId3v1()]));
        l.Add("mp3/dmg-ape-item-overrun.mp3", g, "APEv2: the Album item's value size runs past the end of the tag.",
            () => Mp3(cbr(), [], [Ape(ApeItems().Select(i => i.Key == "Album" ? i with { SizeOverride = 9000 } : i)), StandardId3v1()]));
        l.Add("mp3/dmg-ape-invalid-utf8.mp3", g, "APEv2 text item Artist holding 'Mot\\xF6rhead' in Latin-1, which is not valid UTF-8.",
            () => Mp3(cbr(), [], [Ape([new("Artist", 0, Latin1.GetBytes("Motörhead")), ApeItem.TextItem("Title", "Title")])]));
        return l;
    }

    static byte[] RandomBytes(int n, int seed)
    {
        var b = new byte[n];
        new Random(seed).NextBytes(b);
        return b;
    }

    static IEnumerable<Fixture> Mp3AudioDamage()
    {
        var l = new List<Fixture>();
        const string g = "audio-damage";

        l.Add("mp3/vbr-truncated-half.mp3", g, "VBR file with a Xing header, cut to half its length: the header still claims every frame.",
            () => Seed("mp3-vbr.mp3")[..(Seed("mp3-vbr.mp3").Length / 2)]);
        l.Add("mp3/cbr-truncated-mid-frame.mp3", g, "CBR file whose last frame is cut 100 bytes short.",
            () => Seed("mp3-cbr.mp3")[..^100]);
        l.Add("mp3/vbr-xing-count-doubled.mp3", g, "VBR file whose Xing frame count has been doubled: the header lies.",
            () => PatchXingFrames(Seed("mp3-vbr.mp3"), n => n * 2));
        l.Add("mp3/vbr-xing-zeroed.mp3", g, "VBR file whose Xing header bytes are zeroed: a silent first frame, then VBR audio with no summary.",
            () =>
            {
                var f = Seed("mp3-vbr.mp3");
                MpegHeader.TryParse(f, 0, out var h);
                Array.Clear(f, h.XingOffset, 120);
                return f;
            });
        l.Add("mp3/vbr-zeroed-4k.mp3", g, "VBR file with 4 KiB zeroed in the middle of the audio (bit rot).",
            () =>
            {
                var f = Seed("mp3-vbr.mp3");
                Array.Clear(f, f.Length / 2, 4096);
                return f;
            });
        l.Add("mp3/vbr-bitflips.mp3", g, "VBR file with 40 random single-bit flips in the audio, header bytes included.",
            () =>
            {
                var f = Seed("mp3-vbr.mp3");
                var r = new Random(3);
                for (var i = 0; i < 40; i++)
                {
                    f[r.Next(500, f.Length)] ^= (byte)(1 << r.Next(8));
                }

                return f;
            });
        l.Add("mp3/garbage-prepended.mp3", g, "3000 random bytes, no tag, then a CBR file.",
            () => Concat(RandomBytes(3000, 11), Seed("mp3-cbr.mp3")));
        l.Add("mp3/garbage-appended.mp3", g, "A CBR file followed by 3000 random bytes and no tag.",
            () => Concat(Seed("mp3-cbr.mp3"), RandomBytes(3000, 12)));
        l.Add("mp3/joined-cbr.mp3", g, "Two CBR files joined byte for byte: two Info frames, the first counting only the first file's frames.",
            () => Concat(Seed("mp3-cbr.mp3"), Seed("mp3-cbr-b.mp3")));
        l.Add("mp3/vbri.mp3", g, "The VBR file with its Xing frame rewritten as a Fraunhofer VBRI frame (no LAME tag).",
            () => ToVbri(Seed("mp3-vbr.mp3")));
        l.Add("mp3/vbr-notag-id3v24.mp3", g, "VBR without a Xing header, behind an Id3v2.4 tag: duration needs a scan or an estimate.",
                () => Mp3(Seed("mp3-vbr-notag.mp3"), [Id3v2(4, V24Frames().Take(2))], []))
            .Records("id3v2.4", V24Frames().Take(2));
        return l;
    }

    static byte[] PatchXingFrames(byte[] f, Func<uint, uint> change)
    {
        MpegHeader.TryParse(f, 0, out var h);
        var at = h.XingOffset;
        if (!StartsWith(f, at, "Xing") && !StartsWith(f, at, "Info"))
        {
            throw new InvalidDataException("no Xing tag in seed");
        }

        var flags = ReadU32BE(f, at + 4);
        if ((flags & 1) == 0)
        {
            throw new InvalidDataException("Xing tag has no frame count");
        }

        U32BE(change(ReadU32BE(f, at + 8))).CopyTo(f, at + 8);
        return f;
    }

    static byte[] ToVbri(byte[] f)
    {
        MpegHeader.TryParse(f, 0, out var h);
        var frames = ReadU32BE(f, h.XingOffset + 8);
        var bytes = (uint)f.Length;
        Array.Clear(f, 4, h.FrameLength - 4);
        const int entries = 10;
        var perEntry = (frames + entries - 1) / entries;
        var vbri = Concat(Ascii("VBRI"), U16BE(1), U16BE(576), U16BE(75), U32BE(bytes), U32BE(frames),
            U16BE(entries), U16BE(1), U16BE(2), U16BE(perEntry), Concat(Enumerable.Repeat(U16BE(bytes / entries), entries).ToArray()));
        vbri.CopyTo(f, 36);
        return f;
    }

    // ================================================================================ Ogg

    static List<byte[]> VorbisComments() =>
    [
        Comment("TITLE", "Title ✓"),
        Comment("ARTIST", "AC/DC"),
        Comment("ARTIST", "Motörhead"),
        Comment("ALBUM", "Album"),
        Comment("DATE", "1991-01-02"),
        Comment("TRACKNUMBER", "3"),
        Comment("GENRE", "Rock"),
        Comment("mood", "calm"),
        Comment("COMMENT", "  padded  "),
        Comment("EMPTY", ""),
        Comment("METADATA_BLOCK_PICTURE", Convert.ToBase64String(FlacPicture(Png))),
    ];

    static byte[] WithComments(string seed, IReadOnlyList<byte[]> comments, VorbisCommentOptions? options = null, bool framing = true)
    {
        var opus = seed.EndsWith(".opus");
        var structure = VorbisComment("goro corpus", comments, options);
        var packet = opus ? OpusTagsPacket(structure) : VorbisCommentPacket(structure, framing);
        return Ogg.ReplaceComment(Seed(seed), packet, opus ? 2 : 3);
    }

    static IEnumerable<Fixture> OggFixtures()
    {
        var l = new List<Fixture>();
        foreach (var (seed, ext, codec) in new[] { ("ogg-vorbis.ogg", "ogg", "vorbis"), ("opus.opus", "opus", "opus") })
        {
            var pre = $"ogg/{codec}";
            l.Add($"{pre}-comments.{ext}", "layout", $"Ogg {codec} with the standard comments: ARTIST twice, a lower-case key, an empty value, a padded value, METADATA_BLOCK_PICTURE.",
                    () => WithComments(seed, VorbisComments()))
                .Comments("vorbis", VorbisComments());
            var big = VorbisComments().Append(Comment("LYRICS", new string('l', 150_000))).ToList();
            l.Add($"{pre}-comments-multipage.{ext}", "layout", $"Ogg {codec} whose comment packet is 150 KB, spread over three pages.",
                    () => WithComments(seed, big))
                .Comments("vorbis", big);
            var bad = new List<byte[]> { Comment("TITLE", "Title"), Concat(Ascii("ARTIST="), Latin1.GetBytes("Motörhead")), Ascii("NOEQUALSSIGN"), Comment("BAD~KEY", "tilde is not allowed in a key"), Comment("ALBUM", "Album") };
            l.Add($"{pre}-dmg-comment-values.{ext}", "tag-damage", $"Ogg {codec}: one value is not valid UTF-8, one comment has no '=', one key holds a character the spec forbids.",
                    () => WithComments(seed, bad))
                .Comments("vorbis", bad);
            l.Add($"{pre}-dmg-comment-length-overrun.{ext}", "tag-damage", $"Ogg {codec}: the third comment's length runs past the end of the packet.",
                () => WithComments(seed, VorbisComments(), new() { CommentLengthOverride = (2, 100_000) }));
            l.Add($"{pre}-dmg-comment-count-high.{ext}", "tag-damage", $"Ogg {codec}: the comment count claims 40 comments; there are 11.",
                () => WithComments(seed, VorbisComments(), new() { CountOverride = 40 }));
            l.Add($"{pre}-dmg-vendor-length-overrun.{ext}", "tag-damage", $"Ogg {codec}: the vendor string length runs past the end of the packet.",
                () => WithComments(seed, VorbisComments(), new() { VendorLengthOverride = 1_000_000 }));
            l.Add($"{pre}-dmg-comment-page-crc.{ext}", "tag-damage", $"Ogg {codec}: one byte of a comment value changed after the page CRC was computed.",
                () =>
                {
                    var f = WithComments(seed, VorbisComments());
                    var at = f.AsSpan().IndexOf("Album"u8);
                    f[at] = (byte)'E';
                    return f;
                });
            l.Add($"{pre}-truncated.{ext}", "audio-damage", $"Ogg {codec} cut at 60% of its length: no EOS page, and the last page is incomplete.",
                () => Seed(seed)[..(Seed(seed).Length * 6 / 10)]);
            l.Add($"{pre}-audio-page-crc.{ext}", "audio-damage", $"Ogg {codec} with one byte changed in the middle of an audio page, so its CRC fails.",
                () =>
                {
                    var f = Seed(seed);
                    f[f.Length / 2] ^= 0x55;
                    return f;
                });
            l.Add($"{pre}-last-page-crc.{ext}", "audio-damage", $"Ogg {codec} with the body of the last page damaged, so the page holding the final granule fails its CRC.",
                () =>
                {
                    var f = Seed(seed);
                    f[^5] ^= 0x55;
                    return f;
                });
            l.Add($"{pre}-garbage-appended.{ext}", "audio-damage", $"Ogg {codec} followed by 3000 random bytes.",
                () => Concat(Seed(seed), RandomBytes(3000, 21)));
            l.Add($"{pre}-id3v2-prepended.{ext}", "audio-damage", $"An Id3v2.4 tag before the Ogg {codec} stream, which some tools write.",
                    () => Concat(Id3v2(4, V24Frames().Take(2)), Seed(seed)))
                .Records("id3v2.4", V24Frames().Take(2));
            l.Add($"{pre}-page-missing.{ext}", "audio-damage", $"Ogg {codec} with one audio page in the middle removed: a gap in the page sequence numbers.",
                () =>
                {
                    var f = Seed(seed);
                    var pages = Ogg.Pages(f);
                    var victim = pages[pages.Count / 2];
                    return Concat(f[..(int)victim.Offset], f[(int)(victim.Offset + victim.Length)..]);
                });
        }

        l.Add("ogg/vorbis-dmg-no-framing-bit.ogg", "tag-damage", "Ogg Vorbis comment header without its final framing bit.",
            () => WithComments("ogg-vorbis.ogg", VorbisComments(), framing: false));
        l.Add("ogg/opus-output-gain.opus", "layout", "Ogg Opus with OpusHead output gain set to -6 dB, as loudness tools do: the decoded audio changes.",
            () =>
            {
                var f = Seed("opus.opus");
                var at = f.AsSpan().IndexOf("OpusHead"u8);
                BitConverter.GetBytes((short)(-6 * 256)).CopyTo(f, at + 16);
                var p = Ogg.TryParsePage(f, 0)!;
                return Concat(Ogg.RenderPage(p.Flags, p.Granule, p.Serial, p.Sequence, p.Lacing, f.AsSpan(27 + p.Lacing.Length, p.Body.Length).ToArray()), f[p.Length..]);
            });
        return l;
    }

    // ================================================================================ FLAC

    static byte[] FlacVorbisComment(IReadOnlyList<byte[]> comments, VorbisCommentOptions? options = null) =>
        VorbisComment("goro corpus", comments, options);

    static List<byte[]> FlacComments() => VorbisComments().Take(10).ToList();

    static byte[] FlacWith(Func<List<FlacBlock>, List<(int, byte[])>> blocks, string seed = "flac.flac")
    {
        var f = Seed(seed);
        var (parsed, audio) = Flac.Parse(f);
        return Flac.Build(blocks(parsed), f[audio..]);
    }

    static List<(int, byte[])> Standard(List<FlacBlock> b) =>
    [
        (Flac.StreamInfo, b[0].Data),
        // A piped encode cannot seek back to write one, so the seed may lack it.
        .. b.Where(x => x.Type == Flac.SeekTable).Select(x => (Flac.SeekTable, x.Data)),
        (Flac.VorbisComment, FlacVorbisComment(FlacComments())),
        (Flac.Application, Concat(Ascii("goro"), Ascii("application data"))),
        (Flac.Picture, FlacPicture(Png)),
        (Flac.Padding, new byte[4096]),
    ];

    static IEnumerable<Fixture> FlacFixtures()
    {
        var l = new List<Fixture>();
        l.Add("flac/comments.flac", "layout", "FLAC with STREAMINFO, SEEKTABLE, VORBIS_COMMENT (the standard comments), APPLICATION, PICTURE and PADDING.",
                () => FlacWith(Standard))
            .Comments("vorbis", FlacComments());
        l.Add("flac/comment-block-last.flac", "layout", "FLAC whose VORBIS_COMMENT is the last metadata block, after PADDING.",
                () => FlacWith(b => [(Flac.StreamInfo, b[0].Data), (Flac.Padding, new byte[1024]), (Flac.VorbisComment, FlacVorbisComment(FlacComments()))]))
            .Comments("vorbis", FlacComments());
        var second = new List<byte[]> { Comment("ARTIST", "from the second block") };
        l.Add("flac/two-comment-blocks.flac", "layout", "FLAC with two VORBIS_COMMENT blocks, which the format forbids but files carry.",
                () => FlacWith(b => [(Flac.StreamInfo, b[0].Data), (Flac.VorbisComment, FlacVorbisComment(FlacComments())), (Flac.VorbisComment, FlacVorbisComment(second))]))
            .Comments("vorbis", FlacComments()).Comments("vorbis", second);
        l.Add("flac/id3v2-prefix.flac", "layout", "An Id3v2.4 tag before fLaC, as some rippers write.",
                () => Concat(Id3v2(4, V24Frames().Take(3)), FlacWith(Standard)))
            .Records("id3v2.4", V24Frames().Take(3)).Comments("vorbis", FlacComments());
        l.Add("flac/id3v1-suffix.flac", "layout", "Id3v1.1 after the last frame.",
                () => Concat(FlacWith(Standard), StandardId3v1()))
            .Comments("vorbis", FlacComments());
        l.Add("flac/ape-suffix.flac", "layout", "APEv2 and Id3v1 after the last frame.",
                () => Concat(FlacWith(Standard), Ape(ApeItems()), StandardId3v1()))
            .Comments("vorbis", FlacComments()).Records(ApeItems());
        l.Add("flac/streaminfo-total-zero.flac", "audio-damage", "STREAMINFO total samples 0 and MD5 zero, as an encoder writing to a pipe leaves them (seed flac-piped.flac, retagged).",
            () => FlacWith(Standard, "flac-piped.flac"));
        l.Add("flac/streaminfo-total-doubled.flac", "audio-damage", "STREAMINFO total samples doubled: the header lies.",
            () => FlacWith(b =>
            {
                var s = Standard(b);
                var info = (byte[])s[0].Item2.Clone();
                var total = ((ulong)(info[13] & 0x0F) << 32) | ReadU32BE(info, 14);
                total *= 2;
                info[13] = (byte)((info[13] & 0xF0) | (int)((total >> 32) & 0x0F));
                U32BE((uint)total).CopyTo(info, 14);
                s[0] = (s[0].Item1, info);
                return s;
            }));
        l.Add("flac/truncated.flac", "audio-damage", "FLAC cut at 60% of its length, in the middle of a frame.",
            () => FlacWith(Standard)[..(FlacWith(Standard).Length * 6 / 10)]);
        l.Add("flac/zeroed-4k.flac", "audio-damage", "FLAC with 4 KiB zeroed in the middle of the frames.",
            () =>
            {
                var f = FlacWith(Standard);
                Array.Clear(f, f.Length / 2, 4096);
                return f;
            });
        l.Add("flac/last-frame-damaged.flac", "audio-damage", "FLAC with the last frame's header bytes changed, so its CRC-8 fails.",
            () =>
            {
                var f = FlacWith(Standard);
                for (var i = f.Length - 2; i > 0; i--)
                {
                    if (FlacFrameHeader.TryParse(f, i, out _))
                    {
                        f[i + 4] ^= 0x01;
                        break;
                    }
                }

                return f;
            });
        l.Add("flac/garbage-appended.flac", "audio-damage", "FLAC followed by 3000 random bytes.",
            () => Concat(FlacWith(Standard), RandomBytes(3000, 31)));
        l.Add("flac/dmg-comment-values.flac", "tag-damage", "FLAC VORBIS_COMMENT: invalid UTF-8, a comment without '=', a forbidden key character.",
                () => FlacWith(b => [(Flac.StreamInfo, b[0].Data), (Flac.VorbisComment, FlacVorbisComment([Comment("TITLE", "Title"), Concat(Ascii("ARTIST="), Latin1.GetBytes("Motörhead")), Ascii("NOEQUALSSIGN"), Comment("BAD~KEY", "x")]))]))
            .Comments("vorbis", [Comment("TITLE", "Title"), Concat(Ascii("ARTIST="), Latin1.GetBytes("Motörhead")), Ascii("NOEQUALSSIGN"), Comment("BAD~KEY", "x")]);
        l.Add("flac/dmg-comment-length-overrun.flac", "tag-damage", "FLAC VORBIS_COMMENT: the third comment's length runs past the end of the block.",
            () => FlacWith(b => [(Flac.StreamInfo, b[0].Data), (Flac.VorbisComment, FlacVorbisComment(FlacComments(), new() { CommentLengthOverride = (2, 100_000) })), (Flac.Padding, new byte[512])]));
        l.Add("flac/dmg-block-length-overrun.flac", "tag-damage", "FLAC: the VORBIS_COMMENT block header claims 200 bytes more than the block holds, so the next block header is misread.",
            () =>
            {
                var f = Seed("flac.flac");
                var (parsed, audio) = Flac.Parse(f);
                var vc = FlacVorbisComment(FlacComments());
                return Concat(Ascii("fLaC"), Flac.BlockBytes(0, parsed[0].Data, false), Flac.BlockBytes(4, vc, false, (uint)vc.Length + 200),
                    Flac.BlockBytes(1, new byte[1024], true), f[audio..]);
            });
        l.Add("flac/dmg-no-last-block-flag.flac", "tag-damage", "FLAC whose metadata blocks never set the last-block flag, so a reader walks into the first frame.",
            () =>
            {
                var f = Seed("flac.flac");
                var (parsed, audio) = Flac.Parse(f);
                return Flac.Build([(0, parsed[0].Data), (4, FlacVorbisComment(FlacComments())), (1, new byte[256])], f[audio..], markLast: false);
            });
        l.Add("flac/dmg-zeroed-comment.flac", "tag-damage", "FLAC with 64 bytes in the middle of the VORBIS_COMMENT block zeroed.",
            () =>
            {
                var f = FlacWith(Standard);
                var at = f.AsSpan().IndexOf("ARTIST"u8);
                Array.Clear(f, at - 10, 64);
                return f;
            });
        return l;
    }
}
