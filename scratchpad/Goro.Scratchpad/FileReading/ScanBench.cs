using System.Diagnostics;

namespace Goro.Scratchpad.FileReading;

/// <summary>
/// What does an MP3 frame scan cost next to reading only the head and tail? Warm-cache timings, so
/// they measure CPU and memory traffic; on cold or network storage the bytes read dominate.
/// </summary>
public static class ScanBench
{
    public static void Run(string[] files)
    {
        foreach (var path in files)
        {
            var size = new FileInfo(path).Length;
            Console.WriteLine($"== {Path.GetFileName(path)}: {size / 1e6:0.0} MB");
            Time("read whole file", () => File.ReadAllBytes(path).Length);
            Time("prototype (whole file, scan)", () => (int)FormatReaders.Mp3(File.ReadAllBytes(path)).Durations.Count);
            Time("head + tail, 64 KiB each", () =>
            {
                using var s = File.OpenRead(path);
                var b = new byte[65536];
                var n = s.Read(b);
                s.Seek(-Math.Min(65536, s.Length), SeekOrigin.End);
                return n + s.Read(b);
            });
            Time("TagLibSharp duration", () => (int)TagLib.File.Create(path).Properties.Duration.TotalMilliseconds);
            Time("ATL duration", () => (int)new ATL.Track(path).DurationMs);
            ATL.Settings.MP3_parseExactDuration = true;
            Time("ATL exact duration", () => (int)new ATL.Track(path).DurationMs);
            ATL.Settings.MP3_parseExactDuration = false;
            Time("NLayer duration", () => (int)new NLayer.MpegFile(path).Duration.TotalMilliseconds);
        }
    }

    static void Time(string what, Func<int> run)
    {
        run();
        var times = new List<double>();
        var result = 0;
        for (var i = 0; i < 9; i++)
        {
            var sw = Stopwatch.StartNew();
            result = run();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        Console.WriteLine($"   {what,-30} {times[4],8:0.00} ms  ({result})");
    }
}
