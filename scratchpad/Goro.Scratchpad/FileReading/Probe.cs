using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Goro.Scratchpad.FileReading;

/// <summary>What ffmpeg makes of a file: the reference the duration columns are judged against.</summary>
public sealed record Reference(long? DecodedSamples, int? Rate, int DecodeErrors, double? ProbeSeconds, string? FirstError)
{
    public double? Seconds => DecodedSamples is { } s && Rate is { } r and > 0 ? (double)s / r : null;
}

/// <summary>How faithfully a reader gave back what a fixture records.</summary>
public sealed record Fidelity(int Recorded, int Exact, int ExactBytes, int Renamed, int Altered, int Missing, List<string> Details);

/// <summary>Runs every candidate over the corpus and writes what each one saw.</summary>
public static class Probe
{
    public static void Run(string corpusDir, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var manifest = JsonSerializer.Deserialize<List<ManifestEntry>>(File.ReadAllText(Path.Combine(corpusDir, "manifest.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var files = Directory.EnumerateFiles(corpusDir, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetExtension(p) is ".mp3" or ".ogg" or ".opus" or ".flac")
            .Select(p => Path.GetRelativePath(corpusDir, p).Replace('\\', '/'))
            .OrderBy(p => p.StartsWith("seeds/") ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal)
            .ToList();

        var rows = new List<Row>();
        foreach (var file in files)
        {
            var path = Path.Combine(corpusDir, file);
            var entry = manifest.FirstOrDefault(m => m.File == file);
            var reference = Ffmpeg(path);
            var results = Candidates.All(Path.GetExtension(file)).Select(c => c.Run(path)).ToList();
            var fidelity = entry is { Recorded.Count: > 0 }
                ? results.ToDictionary(r => r.Reader, r => Score(entry.Recorded, r.Fields))
                : [];
            rows.Add(new Row(file, entry?.Group ?? "seed", entry?.Description ?? "encoder-made seed", reference, results, fidelity));
            Console.Error.Write('.');
        }

        Console.Error.WriteLine();
        File.WriteAllText(Path.Combine(outDir, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(outDir, "durations.md"), Durations(rows));
        File.WriteAllText(Path.Combine(outDir, "fidelity.md"), FidelityReport(rows));
        File.WriteAllText(Path.Combine(outDir, "details.md"), Details(rows));
        Console.WriteLine($"{rows.Count} files probed; results in {outDir}");
    }

    public sealed record ManifestEntry(string File, string Group, string Description, List<Recorded> Recorded);

    public sealed record Row(string File, string Group, string Description, Reference Reference, List<CandidateResult> Results, Dictionary<string, Fidelity> Fidelity);

    // ---------------------------------------------------------------- reference

    static Reference Ffmpeg(string path)
    {
        var rateText = RunTool("ffprobe", ["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=sample_rate", "-of", "csv=p=0", path], out _).Trim().Split('\n')[0].Trim(',', ' ');
        var probeText = RunTool("ffprobe", ["-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path], out _).Trim();
        var psi = new ProcessStartInfo("ffmpeg", ["-v", "error", "-i", path, "-map", "0:a:0", "-ac", "1", "-f", "s16le", "-"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        var errTask = p.StandardError.ReadToEndAsync();
        long bytes = 0;
        var buffer = new byte[65536];
        int n;
        var stdout = p.StandardOutput.BaseStream;
        while ((n = stdout.Read(buffer, 0, buffer.Length)) > 0)
        {
            bytes += n;
        }

        p.WaitForExit();
        var errors = errTask.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int? rate = int.TryParse(rateText, out var r) ? r : null;
        double? probe = double.TryParse(probeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        return new Reference(bytes > 0 ? bytes / 2 : null, rate, errors.Length, probe, errors.FirstOrDefault()?.Trim());
    }

    static string RunTool(string tool, string[] args, out string stderr)
    {
        var psi = new ProcessStartInfo(tool, args) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEnd();
        stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return o;
    }

    // ---------------------------------------------------------------- tag fidelity

    static Fidelity Score(List<Recorded> recorded, List<Observed> observed)
    {
        int exact = 0, exactBytes = 0, renamed = 0, altered = 0, missing = 0;
        var details = new List<string>();
        foreach (var rec in recorded.Where(r => r.BytesHex.Length > 0 || r.Values is not null))
        {
            var family = Candidates.Family(rec.Tag);
            var pool = observed.Where(o => o.Tag == family || o.Tag == "*").ToList();
            var hashed = rec.BytesHex.StartsWith("sha256:");
            var recBytes = rec.BytesHex.Length > 0 && !hashed ? Convert.FromHexString(rec.BytesHex) : null;
            var sameKey = pool.Where(o => string.Equals(o.Key, rec.Key, StringComparison.OrdinalIgnoreCase)
                                          && (rec.Description is null || string.Equals(o.Description, rec.Description, StringComparison.Ordinal))).ToList();
            bool BytesMatch(Observed o) => o.Bytes is not null && (hashed ? Corpus.Fingerprint(o.Bytes) == rec.BytesHex : recBytes is not null && o.Bytes.AsSpan().SequenceEqual(recBytes));
            bool ValuesMatch(Observed o) => rec.Values is null ? o.Bytes is not null && recBytes is not null && Contains(o.Bytes, recBytes) : o.Values is not null && o.Values.SequenceEqual(rec.Values);
            var what = $"{rec.Tag} {rec.Key}{(rec.Description is null ? "" : $"[{rec.Description}]")}";
            if (sameKey.Any(ValuesMatch))
            {
                exact++;
                if (sameKey.Any(BytesMatch))
                {
                    exactBytes++;
                }
            }
            else if (pool.FirstOrDefault(ValuesMatch) is { } elsewhere)
            {
                renamed++;
                details.Add($"{what}: found as {elsewhere.Key}{(elsewhere.Description is null ? "" : $"[{elsewhere.Description}]")}");
            }
            else if (sameKey.Count > 0)
            {
                altered++;
                details.Add($"{what}: recorded {Show(rec.Values)} read {string.Join(" | ", sameKey.Select(o => Show(o.Values) + (o.Bytes is null ? "" : $" ({o.Bytes.Length} bytes)")))}");
            }
            else
            {
                missing++;
                details.Add($"{what}: missing (recorded {Show(rec.Values)})");
            }
        }

        return new Fidelity(recorded.Count(r => r.BytesHex.Length > 0 || r.Values is not null), exact, exactBytes, renamed, altered, missing, details);
    }

    static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0 || needle.AsSpan().IndexOf(haystack) >= 0 && haystack.Length > 8;

    static string Show(string[]? values) => values is null ? "(no text)" : "[" + string.Join(", ", values.Select(v => "\"" + Escape(v) + "\"")) + "]";

    static string Escape(string? s)
    {
        if (s is null)
        {
            return "(null)";
        }

        var sb = new StringBuilder();
        foreach (var c in s.Length > 40 ? s[..40] + "…" : s)
        {
            sb.Append(c switch { '\0' => "\\0", '|' => "\\|", < ' ' => $"\\x{(int)c:x2}", _ => c.ToString() });
        }

        return sb.ToString();
    }

    // ---------------------------------------------------------------- reports

    static string Durations(List<Row> rows)
    {
        var readers = rows.SelectMany(r => r.Results.Select(x => x.Reader)).Distinct().ToList();
        var sb = new StringBuilder("# Durations (seconds)\n\nReference: samples ffmpeg 9.0.2 decodes, over the stream's sample rate. `!` marks a reader more than 0.05 s from the reference, `x` an exception or failed read.\n\n");
        sb.Append("| File | ffmpeg decoded | ffprobe claims | " + string.Join(" | ", readers) + " |\n");
        sb.Append("|---|---:|---:|" + string.Concat(readers.Select(_ => "---:|")) + "\n");
        foreach (var r in rows)
        {
            var refSeconds = r.Reference.Seconds;
            sb.Append($"| {r.File} | {Fmt(refSeconds)}{(r.Reference.DecodeErrors > 0 ? $" ({r.Reference.DecodeErrors} err)" : "")} | {Fmt(r.Reference.ProbeSeconds)} |");
            foreach (var reader in readers)
            {
                var x = r.Results.FirstOrDefault(c => c.Reader == reader);
                if (x is null)
                {
                    sb.Append(" |");
                    continue;
                }

                var mark = x.Error is not null ? " x" : refSeconds is { } rs && x.Seconds is { } s && Math.Abs(s - rs) > 0.05 ? " !" : "";
                sb.Append($" {(x.Error is not null ? "" : Fmt(x.Seconds))}{mark} |");
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    static string Fmt(double? s) => s is { } v ? v.ToString("0.000", CultureInfo.InvariantCulture) : "–";

    static string FidelityReport(List<Row> rows)
    {
        string[] readers = ["goro-prototype", "TagLibSharp", "ATL", "TagLibSharp2"];
        var sb = new StringBuilder("# Tag fidelity\n\nPer fixture with recorded data: exact text / recorded (exact bytes), then renamed, altered, missing.\n\n");
        sb.Append("| Fixture | " + string.Join(" | ", readers) + " |\n|---|" + string.Concat(readers.Select(_ => "---|")) + "\n");
        foreach (var r in rows.Where(r => r.Fidelity.Count > 0))
        {
            sb.Append($"| {r.File} |");
            foreach (var reader in readers)
            {
                var res = r.Results.First(x => x.Reader == reader);
                if (res.Error is not null || !r.Fidelity.ContainsKey(reader))
                {
                    sb.Append(" exception |");
                    continue;
                }

                var f = r.Fidelity[reader];
                sb.Append($" {f.Exact}/{f.Recorded} ({f.ExactBytes}b){(f.Renamed > 0 ? $" ren {f.Renamed}" : "")}{(f.Altered > 0 ? $" alt {f.Altered}" : "")}{(f.Missing > 0 ? $" miss {f.Missing}" : "")} |");
            }

            sb.Append('\n');
        }

        sb.Append("\n## What differed\n\n");
        foreach (var r in rows.Where(r => r.Fidelity.Count > 0))
        {
            foreach (var (reader, f) in r.Fidelity.Where(kv => kv.Value.Details.Count > 0 && readers.Contains(kv.Key)))
            {
                sb.Append($"- **{r.File}**, {reader}:\n");
                foreach (var d in f.Details)
                {
                    sb.Append($"  - {d}\n");
                }
            }
        }

        return sb.ToString();
    }

    static string Details(List<Row> rows)
    {
        var sb = new StringBuilder("# Per-file details\n");
        foreach (var r in rows)
        {
            sb.Append($"\n## {r.File}\n\n{r.Description}\n\nffmpeg: {Fmt(r.Reference.Seconds)} s decoded, {r.Reference.DecodeErrors} errors{(r.Reference.FirstError is null ? "" : $" (first: `{r.Reference.FirstError}`)")}; ffprobe claims {Fmt(r.Reference.ProbeSeconds)}\n\n");
            foreach (var x in r.Results)
            {
                sb.Append($"- **{x.Reader}** ({x.Milliseconds:0.0} ms): ");
                if (x.Error is not null)
                {
                    sb.Append($"`{x.Error}`\n");
                    continue;
                }

                sb.Append($"{Fmt(x.Seconds)} s, {x.Fields.Count} fields\n");
                foreach (var n in x.Notes)
                {
                    sb.Append($"  - {n}\n");
                }

                if (r.Group == "tag-damage" || r.Fidelity.Count == 0)
                {
                    foreach (var o in x.Fields.Take(40))
                    {
                        sb.Append($"  - `{o.Tag} {o.Key}{(o.Description is null ? "" : $"[{Escape(o.Description)}]")}` = {Show(o.Values)}{(o.Bytes is null ? "" : $" ({o.Bytes.Length} bytes)")}\n");
                    }
                }
            }
        }

        return sb.ToString();
    }
}
