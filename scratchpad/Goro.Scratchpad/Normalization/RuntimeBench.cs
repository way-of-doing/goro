using System.Text;
using Goro.Scratchpad.Benchmarking;

namespace Goro.Scratchpad.Normalization;

// Goro's normalization against ProposedNormalizer, the candidate its rules were chosen with; and the
// question of whether .NET 10's span-based TryNormalize is worth using instead of string.Normalize.
public static class RuntimeBench
{
    public static void Run()
    {
        INormalizer[] normalizers = [new ProposedNormalizer(), new GoroNormalizer()];
        SmokeTests.Run(normalizers);

        var corpus = TagCorpus.Create();
        var goro = new GoroNormalizer();
        var proposed = new ProposedNormalizer();
        foreach (var (label, inputs) in new[] { ("mixed", corpus.Mixed), ("non-ASCII", corpus.NonAscii) })
        {
            int normalized = inputs.Count(s => goro.Normalize(s) != proposed.Normalize(s));
            int literal = inputs.Count(s => goro.Literal(s) != proposed.Literal(s));
            Console.WriteLine($"{label} corpus: goro and proposed differ on {normalized} normalized, {literal} literal");
        }
        Console.WriteLine();

        Bench.Table("normalization",
            normalizers.Select(n => new Bench.Candidate<INormalizer>(n.Name, n)).ToList(),
            [
                new Bench.Workload<INormalizer, string>("mixed corpus, normalized", corpus.Mixed, (n, s) => n.Normalize(s)),
                new Bench.Workload<INormalizer, string>("ASCII-only corpus, normalized", corpus.AsciiOnly, (n, s) => n.Normalize(s)),
                new Bench.Workload<INormalizer, string>("non-ASCII corpus, normalized", corpus.NonAscii, (n, s) => n.Normalize(s)),
                new Bench.Workload<INormalizer, string>("mixed corpus, LITERALLY", corpus.Mixed, (n, s) => n.Literal(s)),
                new Bench.Workload<INormalizer, string>("non-ASCII corpus, LITERALLY", corpus.NonAscii, (n, s) => n.Literal(s)),
            ]);

        // The Unicode forms alone, on the strings that reach them: what TryNormalize into a stack
        // buffer would save over string.Normalize, which allocates its result.
        var decomposed = corpus.NonAscii.Select(s => s.Normalize(NormalizationForm.FormKD)).ToArray();
        Bench.Table("Unicode forms on non-ASCII strings",
            [
                new Bench.Candidate<bool>("string.Normalize", false),
                new Bench.Candidate<bool>("TryNormalize", true),
            ],
            [
                new Bench.Workload<bool, string>("NFKD of the original", corpus.NonAscii, (span, s) => Form(s, NormalizationForm.FormKD, span)),
                new Bench.Workload<bool, string>("NFC of the decomposed", decomposed, (span, s) => Form(s, NormalizationForm.FormC, span)),
            ]);
    }

    static int written;

    static object? Form(string s, NormalizationForm form, bool span)
    {
        if (!span) return s.Normalize(form);
        Span<char> buffer = stackalloc char[512];
        s.AsSpan().TryNormalize(buffer, out written, form);
        return null;
    }
}
