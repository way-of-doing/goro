using System.Text.Json;

namespace Goro.Scratchpad.FileReading;

/// <summary>
/// Runs the prototype readers over a directory of files from elsewhere (other projects' test data)
/// and writes, as JSON lines, everything they found: tags and fields as recorded, problems, and the
/// duration evidence. Other tools' views are compared with it outside, by script.
/// </summary>
public static class RealWorld
{
    public static void Run(string dir, string output)
    {
        using var w = new StreamWriter(output);
        var n = 0;
        foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order())
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".mp3" or ".mp2" or ".ogg" or ".oga" or ".opus" or ".flac"))
            {
                continue;
            }

            object record;
            try
            {
                var bytes = File.ReadAllBytes(path);
                var r = ext switch
                {
                    ".mp3" or ".mp2" => FormatReaders.Mp3(bytes),
                    ".flac" => FormatReaders.Flac(bytes),
                    _ => FormatReaders.Ogg(bytes),
                };
                record = new
                {
                    path,
                    size = bytes.Length,
                    format = r.Format,
                    durations = r.Durations.Select(d => new { d.Method, seconds = d.Rate > 0 ? d.Seconds : (double?)null, d.Note }),
                    problems = r.Problems,
                    tags = r.Tags.Select(t => new
                    {
                        t.Tag, t.Offset, t.Length, t.StructureProblem, t.IsSource,
                        fields = t.Fields.Select(f => new
                        {
                            f.Key, f.Description, f.Values, f.Problem, f.Quirk,
                            stored = f.Stored.Length,
                            content = f.Content?.Length,
                            head = f.Content is null ? null : Bin.Hex(f.Content, 16),
                        }),
                    }),
                };
            }
            catch (Exception e)
            {
                record = new { path, error = $"{e.GetType().Name}: {e.Message}" };
            }

            w.WriteLine(JsonSerializer.Serialize(record));
            n++;
        }

        Console.WriteLine($"{n} files written to {output}");
    }
}
