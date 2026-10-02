using System.Diagnostics;

namespace Goro.Scratchpad.Benchmarking;

// A rough benchmark: warm up, run each candidate over each workload a few times, report the median
// time and the allocations per item. Good enough to tell 20 ns from 600 ns, not 20 from 25.
public static class Bench
{
    public readonly record struct Result(double NsPerItem, long BytesPerItem);

    // Something to be measured: a name and the object under test.
    public sealed record Candidate<TC>(string Name, TC Subject);

    // A row of the results table: a label, the inputs, and what to do with each input.
    public sealed record Workload<TC, TIn>(string Label, IReadOnlyList<TIn> Inputs, Func<TC, TIn, object?> Op);

    static object? sink;   // keeps results alive so the JIT cannot discard the work

    public static Result Measure<TIn>(IReadOnlyList<TIn> inputs, Func<TIn, object?> op, int runs = 7, int warmups = 2)
    {
        for (int w = 0; w < warmups; w++) foreach (var x in inputs) sink = op(x);
        var times = new List<double>(runs);
        long bytes = 0;
        for (int r = 0; r < runs; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            foreach (var x in inputs) sink = op(x);
            sw.Stop();
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            times.Add(sw.Elapsed.TotalNanoseconds / inputs.Count);
        }
        times.Sort();
        return new Result(times[times.Count / 2], bytes / inputs.Count);
    }

    public static void Table<TC, TIn>(string title, IReadOnlyList<Candidate<TC>> candidates,
        IReadOnlyList<Workload<TC, TIn>> workloads, int runs = 7)
    {
        Console.WriteLine($"{title + $" (ns per item, median of {runs})",-48}" +
                          string.Concat(candidates.Select(c => $"{c.Name,-20}")));
        foreach (var w in workloads)
        {
            var line = $"{w.Label,-48}";
            foreach (var c in candidates)
            {
                var res = Measure(w.Inputs, x => w.Op(c.Subject, x), runs);
                line += $"{res.NsPerItem,7:F0} ({res.BytesPerItem,4} B)".PadRight(20);
            }
            Console.WriteLine(line);
        }
        Console.WriteLine();
    }
}
