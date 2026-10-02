// Entry point of the scratchpad: change freely to run whatever is being tried out.
// Run with: dotnet run -c Release --project scratchpad/Goro.Scratchpad

using Goro.Scratchpad.Benchmarking;
using Goro.Scratchpad.Normalization;

INormalizer[] normalizers = [new ReferenceNormalizer(), new ProposedNormalizer(), new CollationNormalizer()];

SmokeTests.Run(normalizers);

var corpus = TagCorpus.Create();
Bench.Table("normalization",
    normalizers.Select(n => new Bench.Candidate<INormalizer>(n.Name, n)).ToList(),
    [
        new Bench.Workload<INormalizer, string>("mixed corpus, normalized", corpus.Mixed, (n, s) => n.Normalize(s)),
        new Bench.Workload<INormalizer, string>("ASCII-only corpus, normalized", corpus.AsciiOnly, (n, s) => n.Normalize(s)),
        new Bench.Workload<INormalizer, string>("non-ASCII corpus, normalized", corpus.NonAscii, (n, s) => n.Normalize(s)),
        new Bench.Workload<INormalizer, string>("mixed corpus, LITERALLY", corpus.Mixed, (n, s) => n.Literal(s)),
        new Bench.Workload<INormalizer, string>("non-ASCII corpus, LITERALLY", corpus.NonAscii, (n, s) => n.Literal(s)),
    ]);
