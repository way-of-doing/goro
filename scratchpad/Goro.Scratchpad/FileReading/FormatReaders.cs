using static Goro.Scratchpad.FileReading.Bin;

namespace Goro.Scratchpad.FileReading;

/// <summary>One way of knowing a duration, in samples at a rate, with what it rests on.</summary>
public sealed record DurationEvidence(string Method, long Samples, int Rate, string? Note = null)
{
    public double Seconds => Rate == 0 ? double.NaN : (double)Samples / Rate;
    public override string ToString() => $"{Method}={Seconds:0.0000}s" + (Note is null ? "" : $" ({Note})");
}

/// <summary>What a prototype reader found in a file: its tags, where the audio is, and the duration evidence.</summary>
public sealed record FileReport(string Format, List<RawTag> Tags, long AudioStart, long AudioEnd, List<DurationEvidence> Durations, List<string> Problems)
{
    public DurationEvidence? Best(params string[] methods) =>
        methods.Select(m => Durations.FirstOrDefault(d => d.Method == m)).FirstOrDefault(d => d is not null);
}

public static class FormatReaders
{
    public static FileReport Read(string path)
    {
        var f = File.ReadAllBytes(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => Mp3(f),
            ".flac" => Flac(f),
            _ => Ogg(f),
        };
    }

    // ============================================================================ MP3

    public static FileReport Mp3(byte[] f)
    {
        var tags = new List<RawTag>();
        var problems = new List<string>();
        long start = 0, end = f.Length;

        // Leading tags: any number of Id3v2 tags, and an APE tag with a header.
        while (true)
        {
            if (TagReaders.Id3v2(f, (int)start) is { } id3)
            {
                tags.Add(id3);
                if (id3.StructureProblem is { } p)
                {
                    problems.Add($"{id3.Tag}: {p}");
                }

                start = Math.Min(f.Length, id3.Offset + id3.Length);
                continue;
            }

            if (TagReaders.ApeFromHeader(f, (int)start) is { } ape)
            {
                tags.Add(ape);
                start = ape.Offset + ape.Length;
                continue;
            }

            break;
        }

        // Trailing tags, from the end inwards: Id3v1, Lyrics3v2, APE, an appended Id3v2 with a footer.
        while (true)
        {
            if (end - 128 >= start && StartsWith(f, (int)end - 128, "TAG") && tags.All(t => !t.Tag.StartsWith("id3v1")))
            {
                tags.Add(TagReaders.Id3v1(f[..(int)end])!);
                end -= 128;
                continue;
            }

            if (end - 15 >= start && StartsWith(f, (int)end - 9, "LYRICS200") &&
                int.TryParse(System.Text.Encoding.ASCII.GetString(f, (int)end - 15, 6), out var lyricsSize))
            {
                tags.Add(new RawTag("lyrics3v2", end - 15 - lyricsSize, lyricsSize + 15, []));
                end -= 15 + lyricsSize;
                continue;
            }

            if (end - 32 >= start && StartsWith(f, (int)end - 32, "APETAGEX"))
            {
                var ape = TagReaders.ApeFromFooter(f, end)!;
                tags.Add(ape);
                if (ape.StructureProblem is { } p)
                {
                    problems.Add($"ape: {p}");
                }

                end = Math.Max(start, ape.Offset);
                continue;
            }

            if (end - 10 >= start && StartsWith(f, (int)end - 10, "3DI"))
            {
                var size = (int)ReadSyncsafe(f, (int)end - 4);
                var at = (int)end - 20 - size;
                if (at >= start && TagReaders.Id3v2(f, at) is { } appended)
                {
                    tags.Add(appended);
                    end = at;
                    continue;
                }
            }

            break;
        }

        MarkLaterId3v2Tags(tags, problems);
        var durations = new List<DurationEvidence>();
        var first = FindFirstFrame(f, start, end);
        if (first < 0)
        {
            problems.Add("no MPEG audio frame found");
            return new FileReport("mp3", tags, start, end, durations, problems);
        }

        if (first > start)
        {
            problems.Add($"{first - start} bytes before the first frame");
        }

        MpegHeader.TryParse(f, (int)first, out var h);
        var spf = h.SamplesPerFrame;
        var duration = Mp3Duration.Decide(f, first, end, h);
        problems.AddRange(duration.Problems);
        durations.Add(duration.Policy);
        durations.AddRange(duration.Evidence);

        // Evidence for audit only: a walk of every frame, and the estimate naive readers make.
        var scan = Scan(f, first, end, h, duration.FirstIsInfo);
        problems.AddRange(scan.Problems);
        durations.Add(new DurationEvidence("scan", scan.AudioFrames * spf, h.SampleRate, scan.Note));
        if (duration.HasLame)
        {
            durations.Add(new DurationEvidence("scan-gapless", scan.AudioFrames * spf - duration.Delay - duration.Padding, h.SampleRate));
        }

        var audioFrame = duration.FirstIsInfo ? first + (h.FrameLength > 0 ? h.FrameLength : 0) : first;
        if (MpegHeader.TryParse(f, (int)audioFrame, out var ah) && ah.BitrateKbps > 0)
        {
            var bytes = end - audioFrame;
            durations.Add(new DurationEvidence("cbr-estimate", (long)(bytes * 8.0 / (ah.BitrateKbps * 1000) * ah.SampleRate), ah.SampleRate));
        }

        return new FileReport("mp3", tags, start, end, durations, problems);
    }

    /// <summary>
    /// Only the first Id3v2 tag in a file is the <c>id3v2</c> source, as players and other readers
    /// take it; the others are read, so that their extent and damage are known, and reported.
    /// </summary>
    static void MarkLaterId3v2Tags(List<RawTag> tags, List<string> problems)
    {
        var id3 = tags.Where(t => t.Tag.StartsWith("id3v2")).OrderBy(t => t.Offset).ToList();
        if (id3.Count < 2)
        {
            return;
        }

        foreach (var later in id3.Skip(1))
        {
            tags[tags.IndexOf(later)] = later with { IsSource = false };
        }

        problems.Add($"{id3.Count - 1} further Id3v2 tags after the first, which alone is read");
    }

    /// <summary>The first confirmed frame matching the stream between two positions, if any.</summary>
    public static (long At, MpegHeader Header)? FindConfirmedFrame(byte[] f, long from, long to, MpegHeader stream)
    {
        for (var i = from; i + 4 <= to; i++)
        {
            if (f[i] == 0xFF && IsConfirmedFrame(f, i, to, out var h) && h.Matches(stream))
            {
                return (i, h);
            }
        }

        return null;
    }

    /// <summary>A frame header at <paramref name="at"/> confirmed by a matching header where the next frame should start.</summary>
    static bool IsConfirmedFrame(byte[] f, long at, long end, out MpegHeader h)
    {
        if (!MpegHeader.TryParse(f, (int)at, out h) || at + 4 > end)
        {
            return false;
        }

        var length = h.FrameLength > 0 ? h.FrameLength : FreeFormatLength(f, at, end, h);
        if (length <= 0)
        {
            return false;
        }

        var freeFormat = h.FrameLength == 0;
        h = h with { FrameLength = length };
        var next = at + length;
        if (next >= end)
        {
            return !freeFormat; // the last frame: nothing to confirm against
        }

        if (!MpegHeader.TryParse(f, (int)next, out var n) || !n.Matches(h))
        {
            return false;
        }

        // A free-format frame's length is only the distance to the next sync, which junk can fake:
        // it is confirmed only by a third frame at the same distance again, padding aside.
        if (freeFormat)
        {
            var third = next + length;
            var stream = h;
            return n.BitrateKbps == 0 && (FreeAt(third) || FreeAt(third + 1) || FreeAt(third - 1));

            bool FreeAt(long i) => i < end && MpegHeader.TryParse(f, (int)i, out var t) && t.Matches(stream) && t.BitrateKbps == 0;
        }

        return true;
    }

    /// <summary>Free format: the frame ends where the next header with the same fixed bits starts.</summary>
    static int FreeFormatLength(byte[] f, long at, long end, MpegHeader h)
    {
        for (var i = at + 4; i + 4 <= end && i < at + 4000; i++)
        {
            if (f[i] == f[at] && f[i + 1] == f[at + 1] && (f[i + 2] & 0xFC) == (f[at + 2] & 0xFC) && MpegHeader.TryParse(f, (int)i, out _))
            {
                // The padding bit can differ, so the length of this frame is the distance.
                return (int)(i - at);
            }
        }

        return 0;
    }

    static long FindFirstFrame(byte[] f, long start, long end)
    {
        for (var i = start; i + 4 <= end; i++)
        {
            if (f[i] == 0xFF && IsConfirmedFrame(f, i, end, out _))
            {
                return i;
            }
        }

        return -1;
    }

    sealed record ScanResult(long AudioFrames, List<string> Problems, string? Note);

    /// <summary>Walks every frame header from the first frame to the end of the audio.</summary>
    static ScanResult Scan(byte[] f, long first, long end, MpegHeader stream, bool firstIsInfo)
    {
        var problems = new List<string>();
        long frames = 0, infoFrames = 0, resyncs = 0, skipped = 0;
        var bitrates = new HashSet<int>();
        var at = first;
        var isFirst = true;
        while (at + 4 <= end)
        {
            if (MpegHeader.TryParse(f, (int)at, out var h) && h.Matches(stream))
            {
                var length = h.FrameLength > 0 ? h.FrameLength : FreeFormatLength(f, at, end, h);
                if (length <= 0)
                {
                    length = (int)(end - at);
                }

                var xing = (int)at + h.XingOffset;
                var isInfo = StartsWith(f, xing, "Xing") || StartsWith(f, xing, "Info") || StartsWith(f, xing - 2, "Info") || StartsWith(f, xing - 2, "Xing") || StartsWith(f, (int)at + 36, "VBRI");
                if (isInfo && !(isFirst && firstIsInfo))
                {
                    infoFrames++;
                }

                if (!(isFirst && firstIsInfo))
                {
                    frames++;
                    bitrates.Add(h.BitrateKbps);
                }

                if (at + length > end)
                {
                    problems.Add($"last frame truncated by {at + length - end} bytes");
                }

                isFirst = false;
                at += length;
                continue;
            }

            // Lost sync: find the next confirmed frame.
            var next = -1L;
            for (var i = at + 1; i + 4 <= end; i++)
            {
                if (f[i] == 0xFF && IsConfirmedFrame(f, i, end, out var c) && c.Matches(stream))
                {
                    next = i;
                    break;
                }
            }

            resyncs++;
            if (next < 0)
            {
                skipped += end - at;
                break;
            }

            skipped += next - at;
            at = next;
        }

        if (resyncs > 0)
        {
            problems.Add($"lost sync {resyncs} times, skipping {skipped} bytes");
        }

        if (infoFrames > 0)
        {
            problems.Add($"{infoFrames} Xing/Info/VBRI frames after the first: joined files");
        }

        return new ScanResult(frames, problems, bitrates.Count > 1 ? "VBR" : "CBR");
    }

    // ============================================================================ Ogg

    public static FileReport Ogg(byte[] f)
    {
        var tags = new List<RawTag>();
        var problems = new List<string>();
        var durations = new List<DurationEvidence>();
        if (TagReaders.Id3v2(f, 0) is { } id3)
        {
            tags.Add(id3);
            problems.Add("Id3v2 tag before the Ogg stream");
        }

        var pages = FileReading.Ogg.Pages(f);
        if (pages.Count == 0)
        {
            problems.Add("no Ogg page found");
            return new FileReport("ogg", tags, 0, f.Length, durations, problems);
        }

        var bad = pages.Count(p => !p.CrcOk);
        if (bad > 0)
        {
            problems.Add($"{bad} pages fail their CRC");
        }

        var covered = pages.Sum(p => (long)p.Length);
        var leading = pages[0].Offset;
        if (f.Length - covered > 0)
        {
            problems.Add($"{f.Length - covered} bytes outside any page ({leading} before the first)");
        }

        var serials = pages.Select(p => p.Serial).Distinct().ToList();
        // Chained links follow one another; multiplexed streams interleave.
        var spans = serials.Select(s => (Serial: s, First: pages.FindIndex(p => p.Serial == s), Last: pages.FindLastIndex(p => p.Serial == s))).ToList();
        var multiplexed = spans.Any(a => spans.Any(b => a.Serial != b.Serial && a.First < b.First && b.First < a.Last));
        if (serials.Count > 1)
        {
            problems.Add(multiplexed ? $"{serials.Count} multiplexed logical streams" : $"{serials.Count} chained links");
        }

        // Links may differ in sample rate (Vorbis then Opus), so the chain is summed in microseconds.
        long totalMicroseconds = 0;
        var recognised = 0;
        foreach (var (serial, _, _) in spans)
        {
            var mine = pages.Where(p => p.Serial == serial).ToList();
            var gaps = mine.Zip(mine.Skip(1)).Count(x => x.Second.Sequence != x.First.Sequence + 1);
            if (gaps > 0)
            {
                problems.Add($"stream {serial:x8}: {gaps} gaps in page sequence numbers");
            }

            if (!mine[^1].Eos)
            {
                problems.Add($"stream {serial:x8}: no end-of-stream page (truncated?)");
            }

            var packets = FileReading.Ogg.Packets(pages, serial);
            if (packets.Count == 0)
            {
                continue;
            }

            var id = packets[0].Packet;
            string codec;
            int rate;
            long preSkip = 0;
            // The identification headers have fixed lengths: 30 bytes for Vorbis, at least 19 for Opus.
            if (StartsWith(id, 0, "\u0001vorbis") && id.Length >= 30)
            {
                codec = "vorbis";
                rate = (int)ReadU32LE(id, 12);
                if (packets.Count > 1 && StartsWith(packets[1].Packet, 0, "\u0003vorbis"))
                {
                    var p = packets[1].Packet;
                    var c = TagReaders.VorbisComment(p, 7, -1);
                    var framing = 7 + c.Length;
                    if (c.StructureProblem is null && (framing >= p.Length || (p[framing] & 1) == 0))
                    {
                        c = c with { StructureProblem = "framing bit not set" };
                    }

                    tags.Add(c);
                }
            }
            else if (StartsWith(id, 0, "OpusHead") && id.Length >= 19)
            {
                codec = "opus";
                rate = 48000;
                preSkip = ReadU16LE(id, 10);
                if (packets.Count > 1 && StartsWith(packets[1].Packet, 0, "OpusTags"))
                {
                    tags.Add(TagReaders.VorbisComment(packets[1].Packet, 8, -1));
                }
            }
            else
            {
                problems.Add($"stream {serial:x8}: codec not recognised");
                continue;
            }

            if (tags.LastOrDefault() is { Offset: -1, StructureProblem: { } commentProblem })
            {
                problems.Add($"{codec} comment: {commentProblem}");
            }

            var good = mine.Where(p => p.CrcOk && p.Granule >= 0).ToList();
            var any = mine.Where(p => p.Granule >= 0).ToList();
            if (good.Count == 0)
            {
                continue;
            }

            var lastGood = good[^1].Granule;
            var lastAny = any[^1].Granule;
            durations.Add(new DurationEvidence($"{codec}-granule", lastGood - preSkip, rate, preSkip > 0 ? $"pre-skip {preSkip}" : null));
            if (lastAny != lastGood)
            {
                durations.Add(new DurationEvidence($"{codec}-granule-unchecked", lastAny - preSkip, rate, "last page fails its CRC"));
            }

            totalMicroseconds += (lastGood - preSkip) * 1_000_000 / rate;
            recognised++;
        }

        if (serials.Count > 1 && !multiplexed)
        {
            durations.Insert(0, new DurationEvidence("chain-sum", totalMicroseconds, 1_000_000));
        }

        // The policy: the last granule on a page that passes its CRC; the sum over chained links; no
        // answer for multiplexed streams, where which stream is "the audio" is not the file's to say.
        durations.Insert(0, multiplexed
            ? new DurationEvidence("policy", 0, 0, "multiplexed: declined")
            : recognised == 0
            ? new DurationEvidence("policy", 0, 0, "no stream in a codec Goro reads")
            : new DurationEvidence("policy", totalMicroseconds, 1_000_000, serials.Count > 1 ? "sum of chained links" : "last granule on a page passing its CRC"));

        return new FileReport("ogg", tags, 0, f.Length, durations, problems);
    }

    static uint ReadU16LE(byte[] b, int at) => (uint)(b[at] | b[at + 1] << 8);

    // ============================================================================ FLAC

    public static FileReport Flac(byte[] f)
    {
        var tags = new List<RawTag>();
        var problems = new List<string>();
        var durations = new List<DurationEvidence>();
        var at = 0;
        while (TagReaders.Id3v2(f, at) is { } id3)
        {
            tags.Add(id3);
            problems.Add("Id3v2 tag before fLaC");
            at = (int)Math.Min(f.Length, id3.Offset + id3.Length);
        }

        MarkLaterId3v2Tags(tags, problems);
        if (!StartsWith(f, at, "fLaC"))
        {
            problems.Add("no fLaC marker");
            return new FileReport("flac", tags, at, f.Length, durations, problems);
        }

        long end = f.Length;
        if (end - 128 > at && StartsWith(f, (int)end - 128, "TAG"))
        {
            tags.Add(TagReaders.Id3v1(f)!);
            end -= 128;
        }

        if (end - 32 > at && StartsWith(f, (int)end - 32, "APETAGEX") && TagReaders.ApeFromFooter(f, end) is { } ape)
        {
            tags.Add(ape);
            end = ape.Offset;
        }

        // Metadata blocks, trusting nothing: each length must fit, and a frame sync where a block
        // header should be means the last-block flag was never set.
        var pos = at + 4;
        byte[]? streamInfo = null;
        var lastSeen = false;
        while (pos + 4 <= end)
        {
            if (FlacFrameHeader.TryParse(f, pos, out _) && streamInfo is not null)
            {
                problems.Add("no block carries the last-block flag; a frame follows the last block read");
                break;
            }

            var last = (f[pos] & 0x80) != 0;
            var type = f[pos] & 0x7F;
            var length = (int)ReadU24BE(f, pos + 1);
            if (type == 127 || pos + 4 + length > end)
            {
                problems.Add($"metadata block at {pos} (type {type}, length {length}) is not well formed");
                break;
            }

            var data = f.AsSpan(pos + 4, length).ToArray();
            if (type == 0 && streamInfo is null)
            {
                streamInfo = data;
            }
            else if (type == 0)
            {
                problems.Add($"a second STREAMINFO at {pos}: the block chain has been lost");
                break;
            }
            else if (type == 4)
            {
                var c = TagReaders.VorbisComment(data, 0, pos + 4);
                if (c.StructureProblem is { } p)
                {
                    problems.Add($"vorbis comment: {p}");
                }

                tags.Add(c);
            }

            pos += 4 + length;
            if (last)
            {
                lastSeen = true;
                break;
            }
        }

        var audioStart = pos;
        if (!FlacFrameHeader.TryParse(f, audioStart, out var firstFrame))
        {
            problems.Add(lastSeen ? "no frame where the metadata ends" : "audio start not found");
            // Fall back to a search for the first frame after the metadata.
            for (var i = at + 4; i + 6 <= end; i++)
            {
                if (FlacFrameHeader.TryParse(f, i, out firstFrame) && firstFrame.Number == 0)
                {
                    audioStart = i;
                    break;
                }
            }
        }

        if (streamInfo is null || streamInfo.Length < 34)
        {
            problems.Add("no STREAMINFO");
            return new FileReport("flac", tags, audioStart, end, durations, problems);
        }

        var rate = (int)(ReadU24BE(streamInfo, 10) >> 4);
        var total = ((long)(streamInfo[13] & 0x0F) << 32) | ReadU32BE(streamInfo, 14);
        var minBlock = (int)ReadU16BE(streamInfo, 0);
        var maxBlock = (int)ReadU16BE(streamInfo, 2);
        var maxFrame = (int)ReadU24BE(streamInfo, 7);
        if (total > 0)
        {
            durations.Add(new DurationEvidence("streaminfo", total, rate));
        }
        else
        {
            problems.Add("STREAMINFO total samples is 0 (unknown)");
        }

        // The last intact frame: search back from the end of the audio for a header that passes its
        // CRC-8 and a frame that passes its CRC-16, giving up on the tail after a bounded distance.
        var nominal = minBlock == maxBlock ? maxBlock : firstFrame.BlockSize;
        var frameEnd = end;
        var limit = Math.Max(audioStart, end - Math.Max(4 * Math.Max(maxFrame, 16384), 1 << 18));
        var notes = new List<string>();
        for (var i = end - 6; i >= limit; i--)
        {
            if (!FlacFrameHeader.TryParse(f, (int)i, out var last) || last.BlockSize > Math.Max(maxBlock, 1))
            {
                continue;
            }

            var crcEnd = FrameEndByCrc16(f, i, frameEnd, last.Length);
            if (crcEnd < 0)
            {
                notes.Add($"frame at {i} fails its CRC-16");
                frameEnd = i;
                continue;
            }

            frameEnd = crcEnd;

            var endSample = last.VariableBlocking ? (long)last.Number + last.BlockSize : (long)last.Number * nominal + last.BlockSize;
            durations.Add(new DurationEvidence("last-frame", endSample, rate, notes.Count == 0 ? null : string.Join("; ", notes)));
            if (frameEnd != end)
            {
                problems.Add($"{end - frameEnd} bytes after the end of the last intact frame");
            }

            break;
        }

        var declared = durations.FirstOrDefault(d => d.Method == "streaminfo");
        var observed = durations.FirstOrDefault(d => d.Method == "last-frame");
        // A tail too short to hold the samples STREAMINFO says are missing is a truncation; a tail
        // long enough is damage to the last frames, which leaves STREAMINFO standing.
        var tailHoldsMissing = declared is { } dd && observed is { } oo && dd.Samples > oo.Samples && oo.Samples > 0 &&
                               (end - frameEnd) >= 0.5 * (dd.Samples - oo.Samples) * ((double)(frameEnd - audioStart) / oo.Samples);
        durations.Insert(0, (declared, observed) switch
        {
            ({ } d, { } o) when d.Samples == o.Samples => d with { Method = "policy", Note = "STREAMINFO, confirmed by the last frame" },
            ({ } d, { }) when tailHoldsMissing => d with { Method = "policy", Note = "STREAMINFO; the tail after the last intact frame is damaged" },
            (_, { } o) => o with { Method = "policy", Note = declared is null ? "no STREAMINFO total: last frame" : "STREAMINFO disagrees: last frame" },
            ({ } d, null) => d with { Method = "policy", Note = "STREAMINFO, unconfirmed: no intact last frame" },
            _ => new DurationEvidence("policy", 0, 0, "unknown"),
        });

        return new FileReport("flac", tags, audioStart, end, durations, problems);
    }

    /// <summary>
    /// Where the frame at <paramref name="at"/> ends: the first position at which the CRC-16 of what
    /// precedes it matches the two bytes there, or -1 if none does before <paramref name="limit"/>.
    /// </summary>
    static long FrameEndByCrc16(byte[] f, long at, long limit, int headerLength)
    {
        ushort crc = 0;
        for (var k = at; k + 2 <= limit; k++)
        {
            if (k >= at + headerLength + 1 && crc == ReadU16BE(f, (int)k))
            {
                return k + 2;
            }

            crc ^= (ushort)(f[k] << 8);
            for (var b = 0; b < 8; b++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x8005) : (ushort)(crc << 1);
            }
        }

        return -1;
    }

    /// <summary>Whether the frame at <paramref name="at"/>, running to <paramref name="end"/>, ends with a matching CRC-16.</summary>
    static bool FrameCrc16Ok(byte[] f, long at, long end) =>
        end - at > 2 && Crc16(f.AsSpan((int)at, (int)(end - at - 2))) == ReadU16BE(f, (int)end - 2);
}
