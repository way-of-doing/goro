using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// The reader never throws over what a file holds (docs/implementation.md). Passing the corpus does
/// not show it: the prototype passed it, and a fuzzer damaging its files still found 16 exceptions.
/// So every MP3 in the corpus is truncated and overwritten at random, with a fixed seed so that a
/// failure can be repeated, and read whole: the analysis, and every field's bytes, text and
/// description. Half the runs use tiny windows, so that reads outside them, and refusals, are met too.
/// </summary>
public class ReaderFuzzTests
{
    private const int RunsPerFile = 50;

    private static readonly ReadPolicy Tiny = new(HeadWindow: 64, TailWindow: 64, SniffBudget: 64, SearchBudget: 4096, StructureBudget: 1024, PayloadLimit: 512);

    private static IEnumerable<TestCaseData> Files() =>
        AudioCorpus.Mp3Files.Select((file, index) => new TestCaseData(file, index).SetName($"fuzz: {file}"));

    [TestCaseSource(nameof(Files))]
    public void DamagedAtRandom_TheReaderNeverThrows(string file, int seed)
    {
        var original = AudioCorpus.Bytes(file);
        var random = new Random(seed);
        for (var run = 0; run < RunsPerFile; run++)
        {
            var damaged = Damage(original, random);
            var policy = run % 2 == 0 ? ReadPolicy.Default : Tiny;
            Assert.DoesNotThrow(() => ReadEverything(damaged, policy), $"run {run}");
        }
    }

    /// <summary>However a file is damaged, its analysis takes no more from it than the policy's ceiling.</summary>
    [TestCaseSource(nameof(Files))]
    public void DamagedAtRandom_TheAnalysisStaysWithinItsBudgets(string file, int seed)
    {
        var original = AudioCorpus.Bytes(file);
        var random = new Random(seed);
        for (var run = 0; run < RunsPerFile; run++)
        {
            var damaged = Damage(original, random);
            var policy = run % 2 == 0 ? ReadPolicy.Default : Tiny;
            var reader = new BoundedReader(new MemoryByteSource(damaged), policy);
            Mp3Analysis.Analyse(reader);
            Assert.That(reader.Log.BytesFromSource, Is.LessThanOrEqualTo(policy.AnalysisCeiling), $"run {run}");
        }
    }

    private static void ReadEverything(byte[] bytes, ReadPolicy policy)
    {
        var reader = new BoundedReader(new MemoryByteSource(bytes), policy);
        var layout = Mp3Analysis.Analyse(reader);
        var values = new TagValues(reader);
        foreach (var field in layout.Tags.SelectMany(tag => tag.Fields))
        {
            values.Bytes(field);
            values.Text(field);
            values.Description(field);
        }
    }

    /// <summary>One of: cut short, a few bytes overwritten, a block overwritten, or a block of zeroes or of 0xFF.</summary>
    private static byte[] Damage(byte[] original, Random random)
    {
        var bytes = (byte[])original.Clone();
        switch (random.Next(5))
        {
            case 0:
                return bytes[..random.Next(bytes.Length)];
            case 1:
                for (var i = random.Next(1, 16); i > 0; i--)
                {
                    bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
                }

                return bytes;
            case 2:
                var start = random.Next(bytes.Length);
                random.NextBytes(bytes.AsSpan(start, Math.Min(bytes.Length - start, random.Next(1, 256))));
                return bytes;
            default:
                var at = random.Next(bytes.Length);
                bytes.AsSpan(at, Math.Min(bytes.Length - at, random.Next(1, 256))).Fill(random.Next(2) == 0 ? (byte)0 : (byte)0xFF);
                return bytes;
        }
    }
}
