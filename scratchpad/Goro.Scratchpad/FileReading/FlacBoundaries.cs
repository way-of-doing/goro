using static Goro.Scratchpad.FileReading.Bin;

namespace Goro.Scratchpad.FileReading;

/// <summary>
/// Where the candidate edges of a FLAC file's audio fall, for every FLAC file in the corpus, by the
/// positions of docs/design/flac-layout.md: the end of the leading tags (B), the fLaC marker (C),
/// where the metadata block chain says it ends (K), the first frame found by search (D), the end of
/// the last whole frame (E), the start of the trailing tags (F) and the end of the file (H). Beside
/// them, TagLibSharp's invariant range, which is what TagLibSharp would hash.
///
/// It also counts what makes finding frames hard: chance frame headers (sync and a passing CRC-8)
/// outside the audio, and positions inside a frame where the running CRC-16 matches the next two
/// bytes, which is how a reader that delimits frames by CRC-16 alone would end a frame too early.
/// </summary>
public static class FlacBoundaries
{
    public static void Run(string corpusDir)
    {
        Console.WriteLine($"{"file",-40} {"H",6} {"B",5} {"C",5} {"K",6} {"D",6} {"E",6} {"F",6}  {"taglib",-15} {"frames",6} {"bad16",5} {"resync",6} {"hdr≠D",5} {"crc16@",6}  chain");
        var files = Directory.EnumerateFiles(corpusDir, "*.flac", SearchOption.AllDirectories)
            .OrderBy(p => p.Contains("seeds") ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal);
        foreach (var path in files)
        {
            var f = File.ReadAllBytes(path);
            string taglib;
            try
            {
                using var t = TagLib.File.Create(path);
                taglib = $"[{t.InvariantStartPosition},{t.InvariantEndPosition})";
            }
            catch (Exception e)
            {
                taglib = e.GetType().Name[..Math.Min(15, e.GetType().Name.Length)];
            }

            var r = Measure(f);
            Console.WriteLine($"{Path.GetRelativePath(corpusDir, path),-40} {f.Length,6} {r.B,5} {Show(r.C),5} {Show(r.K),6} {Show(r.D),6} {Show(r.E),6} {r.F,6}  {taglib,-15} {r.Frames,6} {r.BadCrc16,5} {r.Resyncs,6} {r.StrayHeaders,5} {r.EarlyCrc16,6}  {r.Chain}");
        }
    }

    static string Show(long v) => v < 0 ? "-" : v.ToString();

    public sealed record Positions(long B, long C, long K, long D, long E, long F, int Frames, int BadCrc16, int Resyncs, int StrayHeaders, int EarlyCrc16, string Chain);

    public static Positions Measure(byte[] f)
    {
        // B: the leading Id3v2 tags, by their declared sizes.
        long b = 0;
        while (TagReaders.Id3v2(f, (int)b) is { } id3)
        {
            b = Math.Min(f.Length, id3.Offset + id3.Length);
        }

        // F: the trailing tags, from the end inwards.
        long fEnd = f.Length;
        if (fEnd - 128 >= b && StartsWith(f, (int)fEnd - 128, "TAG"))
        {
            fEnd -= 128;
        }

        if (fEnd - 32 >= b && StartsWith(f, (int)fEnd - 32, "APETAGEX") && TagReaders.ApeFromFooter(f, fEnd) is { } ape)
        {
            fEnd = ape.Offset;
        }

        // C: the marker, where B says, or found by search.
        long c = StartsWith(f, (int)b, "fLaC") ? b : IndexOf(f, "fLaC"u8, b);

        // K: the end of the block chain, trusting every declared length, as libFLAC does.
        long k = -1;
        var chain = new List<string>();
        if (c >= 0)
        {
            var at = c + 4;
            while (true)
            {
                if (at + 4 > f.Length)
                {
                    chain.Add("EOF");
                    break;
                }

                var last = (f[at] & 0x80) != 0;
                var type = f[at] & 0x7F;
                var length = ReadU24BE(f, (int)at + 1);
                chain.Add(type switch
                {
                    0 => "SI", 1 => "PAD", 2 => "APP", 3 => "SEEK", 4 => "VC", 5 => "CUE", 6 => "PIC", 127 => "127!",
                    _ => $"t{type}",
                } + $"({length})" + (last ? "*" : ""));
                if (type == 127 || at + 4 + length > f.Length)
                {
                    chain.Add("broken");
                    break;
                }

                at += 4 + length;
                if (last)
                {
                    k = at;
                    break;
                }
            }
        }

        // D: the first frame by search, from just after the marker: a header that passes its CRC-8
        // and numbers the first frame or sample, confirmed by the next frame's header.
        long d = -1;
        var stray = 0;
        for (var i = c >= 0 ? c + 4 : b; i + 6 <= fEnd && d < 0; i++)
        {
            if (FlacFrameHeader.TryParse(f, (int)i, out var h))
            {
                if (h.Number == 0 && NextHeader(f, i, h, fEnd, out _, out _))
                {
                    d = i;
                }
                else
                {
                    stray++;
                }
            }
        }

        // E: walk frame to frame, each next header found by search and checked against the
        // numbering; over damage, resynchronise on any later number.
        long e = -1;
        int frames = 0, badCrc16 = 0, resyncs = 0, early = 0;
        if (d >= 0)
        {
            var s = d;
            FlacFrameHeader.TryParse(f, (int)s, out var cur);
            while (NextHeader(f, s, cur, fEnd, out var next, out var nh))
            {
                frames++;
                if (Crc16(f.AsSpan((int)s, (int)(next - s - 2))) != ReadU16BE(f, (int)next - 2))
                {
                    badCrc16++;
                }
                else
                {
                    early += EarlyMatches(f, s, next);
                }

                if (nh.Number != Expected(cur))
                {
                    resyncs++;
                }

                s = next;
                cur = nh;
            }

            // The last frame ends where its CRC-16 matches; prefer the end of the audio.
            frames++;
            if (s + 2 < fEnd && Crc16(f.AsSpan((int)s, (int)(fEnd - s - 2))) == ReadU16BE(f, (int)fEnd - 2))
            {
                e = fEnd;
                early += EarlyMatches(f, s, fEnd);
            }
            else
            {
                e = s;
                for (var end = s + cur.Length + 3; end <= fEnd; end++)
                {
                    if (Crc16(f.AsSpan((int)s, (int)(end - s - 2))) == ReadU16BE(f, (int)end - 2))
                    {
                        e = end;
                        break;
                    }
                }

                if (e == s)
                {
                    badCrc16++;
                }
            }
        }

        // Stray headers after the audio too: chance syncs in whatever follows E.
        for (var i = Math.Max(e, 0); e >= 0 && i + 6 <= f.Length; i++)
        {
            if (FlacFrameHeader.TryParse(f, (int)i, out _))
            {
                stray++;
            }
        }

        return new Positions(b, c, k, d, e, fEnd, frames, badCrc16, resyncs, stray, early, string.Join(" ", chain));
    }

    static ulong Expected(FlacFrameHeader h) => h.VariableBlocking ? h.Number + (ulong)h.BlockSize : h.Number + 1;

    /// <summary>The next frame header after the one at <paramref name="at"/>: the expected number, or any later one within 64 frames.</summary>
    static bool NextHeader(byte[] f, long at, FlacFrameHeader cur, long limit, out long next, out FlacFrameHeader h)
    {
        var want = Expected(cur);
        var slack = cur.VariableBlocking ? 64UL * (ulong)cur.BlockSize : 64UL;
        for (var i = at + cur.Length + 2; i + 6 <= limit; i++)
        {
            if (f[i] == 0xFF && FlacFrameHeader.TryParse(f, (int)i, out h) && h.VariableBlocking == cur.VariableBlocking &&
                h.Number >= want && h.Number <= want + slack)
            {
                next = i;
                return true;
            }
        }

        next = -1;
        h = default;
        return false;
    }

    /// <summary>Positions inside the frame [s, end) where the running CRC-16 matches the two bytes that follow, before the true end.</summary>
    static int EarlyMatches(byte[] f, long s, long end)
    {
        ushort crc = 0;
        var n = 0;
        for (var k = s; k + 2 < end; k++)
        {
            if (k > s + 6 && crc == ReadU16BE(f, (int)k))
            {
                n++;
            }

            crc ^= (ushort)(f[k] << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x8005) : (ushort)(crc << 1);
            }
        }

        return n;
    }

    static long IndexOf(byte[] f, ReadOnlySpan<byte> what, long from)
    {
        var i = f.AsSpan((int)from).IndexOf(what);
        return i < 0 ? -1 : from + i;
    }
}
