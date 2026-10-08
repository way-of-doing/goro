namespace Goro.Scratchpad.FileReading;

/// <summary>
/// Entry point of the file-reading survey. Paths are relative to the repository root, which is
/// where `dotnet run --project scratchpad/Goro.Scratchpad -- ...` is expected to run from.
///
///   corpus                rebuild tests/Goro.Tests/Fixtures/Audio from its seeds
///   probe <out dir>       run every candidate reader over the corpus; write durations.md,
///                         fidelity.md, details.md and results.json
///   read <file>...        the prototype's duration evidence and problems for each file
///   bench <mp3>...        what a full MP3 scan costs next to the libraries
///   taglib-hook           what TagLibSharp's FrameFactory hook is handed
///   realworld <dir> <out>  dump what the prototype reads from every audio file under a directory
///   boundaries            where the candidate edges of each MP3's audio fall
///   fuzz [rounds]         truncate and overwrite every corpus file; the readers must not throw
/// </summary>
public static class Survey
{
    public const string CorpusDir = "tests/Goro.Tests/Fixtures/Audio";
    public static string SeedDir => Path.Combine(CorpusDir, "seeds");

    public static void Run(string[] args)
    {
        switch (args.FirstOrDefault())
        {
            case "corpus":
                Corpus.Write(CorpusDir, SeedDir);
                break;
            case "probe":
                Probe.Run(CorpusDir, args.ElementAtOrDefault(1) ?? "probe-results");
                break;
            case "taglib-hook":
                TagLibHook.Run(CorpusDir);
                break;
            case "bench":
                ScanBench.Run(args[1..]);
                break;
            case "candidates":
                foreach (var path in args[1..])
                {
                    Console.WriteLine($"== {Path.GetFileName(path)}");
                    foreach (var (name, run) in Candidates.All(Path.GetExtension(path).ToLowerInvariant()))
                    {
                        var r = run(path);
                        var fields = r.Fields.Where(f => f.Key is "TIT2" or "TALB" or "TPE1" or "Title" or "Album" or "Artist" or "COMM")
                            .Select(f => $"{f.Key}{(f.Description is null ? "" : $"[{f.Description}]")}={string.Join("|", (f.Values ?? []).Select(v => v?.Replace("\0", "\\0") ?? "null"))}");
                        Console.WriteLine($"   {name,-22} {r.Error ?? string.Join("  ", fields)}");
                    }
                }

                break;
            case "realworld":
                RealWorld.Run(args[1], args[2]);
                break;
            case "boundaries":
                Boundaries.Run(CorpusDir);
                break;
            case "fuzz":
                Fuzz.Run(CorpusDir, int.Parse(args.ElementAtOrDefault(1) ?? "200"));
                break;
            case "read":
                foreach (var path in args[1..])
                {
                    var r = FormatReaders.Read(path);
                    Console.WriteLine($"{Path.GetFileName(path)}: {string.Join(", ", r.Durations)}; {string.Join("; ", r.Problems)}");
                }

                break;
            default:
                Console.WriteLine("usage: corpus | probe <out dir> | read <file>... | bench <mp3>... | taglib-hook | fuzz [rounds]");
                break;
        }
    }
}
