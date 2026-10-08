namespace Goro.Scratchpad.FileReading;

/// <summary>
/// The prototype readers claim never to throw on damaged input. This checks the claim: every
/// corpus file, truncated at many points and with bytes overwritten at random, must read without
/// an exception.
/// </summary>
public static class Fuzz
{
    public static void Run(string corpusDir, int rounds)
    {
        var rng = new Random(42);
        var failures = new Dictionary<string, int>();
        var runs = 0;
        foreach (var path in Directory.EnumerateFiles(corpusDir, "*", SearchOption.AllDirectories)
                     .Where(p => Path.GetExtension(p) is ".mp3" or ".ogg" or ".opus" or ".flac"))
        {
            var original = File.ReadAllBytes(path);
            for (var i = 0; i < rounds; i++)
            {
                var f = rng.Next(2) == 0
                    ? original[..rng.Next(original.Length)]
                    : Mutate(original, rng);
                runs++;
                try
                {
                    _ = Path.GetExtension(path) switch
                    {
                        ".mp3" => FormatReaders.Mp3(f),
                        ".flac" => FormatReaders.Flac(f),
                        _ => FormatReaders.Ogg(f),
                    };
                }
                catch (Exception e)
                {
                    var key = $"{e.GetType().Name} at {string.Join(" <- ", (e.StackTrace ?? "").Split('\n').Where(l => l.Contains("FileReading")).Take(2).Select(l => l.Trim()[(l.Trim().LastIndexOf('.', l.Trim().IndexOf('(')) + 1)..].Split(" in ")[0] + ":" + l.Split(':').Last().Trim()))}";
                    failures[key] = failures.GetValueOrDefault(key) + 1;
                    if (failures[key] == 1 && Environment.GetEnvironmentVariable("FUZZ_DUMP") is { } dump)
                    {
                        File.WriteAllBytes(Path.Combine(dump, $"fail{failures.Count}{Path.GetExtension(path)}"), f);
                        Console.WriteLine($"fail{failures.Count}: {path}: {e}");
                    }
                }
            }
        }

        Console.WriteLine($"{runs} damaged reads, {failures.Values.Sum()} exceptions");
        foreach (var (k, n) in failures.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"  {n,5}  {k}");
        }
    }

    static byte[] Mutate(byte[] original, Random rng)
    {
        var f = (byte[])original.Clone();
        var n = rng.Next(1, 20);
        for (var i = 0; i < n; i++)
        {
            // Bias towards the start and the end, where the tags and headers are.
            var at = rng.Next(3) switch
            {
                0 => rng.Next(Math.Min(f.Length, 4096)),
                1 => f.Length - 1 - rng.Next(Math.Min(f.Length, 4096)),
                _ => rng.Next(f.Length),
            };
            f[at] = (byte)rng.Next(256);
        }

        return f;
    }
}
