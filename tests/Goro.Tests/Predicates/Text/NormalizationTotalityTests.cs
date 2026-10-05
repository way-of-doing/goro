using System.Text;
using Goro.Predicates.Text;

namespace Goro.Tests.Predicates.Text;

// Normalization is total: no input, however malformed, may make it throw, since a run must never stop
// because a tag holds text that is not valid UTF-16. Step 1 repairs unpaired surrogates; U+FFFE, which
// .NET's own normalization refuses, must come through as well.
public class NormalizationTotalityTests
{
    private static readonly ComparisonMode[] Modes = [ComparisonMode.Normalized, ComparisonMode.Literal];

    // Not attributes: a custom attribute cannot hold a lone surrogate, and the test adapter silently
    // drops a case whose name holds U+FFFE; see EscapedTestCase.
    private static IEnumerable<TestCaseData> UnpairedSurrogates =>
    [
        EscapedTestCase.Of("abc\uD800def", "abc\uFFFDdef"),
        EscapedTestCase.Of("abc\uDC00def", "abc\uFFFDdef"),
        EscapedTestCase.Of("\uDC00\uD800", "\uFFFD\uFFFD"),   // a pair the wrong way round is two unpaired surrogates
        EscapedTestCase.Of("abc\uD800", "abc\uFFFD"),
        EscapedTestCase.Of("\uD800\uD800\uDC00", "\uFFFD\U00010000"),
        EscapedTestCase.Of("\uFFFE\uD800", "\uFFFE\uFFFD"),
    ];

    [TestCaseSource(nameof(UnpairedSurrogates))]
    public void UnpairedSurrogatesBecomeReplacementCharacters(string text, string repaired)
    {
        foreach (var mode in Modes)
            Assert.That(Normalization.Prepare(text, mode), Is.EqualTo(repaired), mode.ToString());
    }

    [Test]
    public void SurrogatePairsAreKept()
    {
        foreach (var mode in Modes)
            Assert.That(Normalization.Prepare("\U0001F600", mode), Is.EqualTo("\U0001F600"), mode.ToString());
    }

    private static IEnumerable<TestCaseData> NoncharacterCases =>
    [
        EscapedTestCase.Of("\uFFFE", "\uFFFE"),
        EscapedTestCase.Of("\uFFFF", "\uFFFF"),
        EscapedTestCase.Of("Mo\uFFFEto\u0308rhead", "mo\uFFFEtorhead"),
        EscapedTestCase.Of("e\uFFFE\u0301", "e\uFFFE\u0301"),   // the noncharacter, not e, is the mark's base
    ];

    [TestCaseSource(nameof(NoncharacterCases))]
    public void NoncharacterFFFE_IsNormalizedAround(string text, string prepared) =>
        Assert.That(Normalization.Prepare(text, ComparisonMode.Normalized), Is.EqualTo(prepared));

    private static IEnumerable<TestCaseData> LiteralNoncharacterCases =>
    [
        EscapedTestCase.Of("Mo\uFFFEto\u0308rhead", "Mo\uFFFEt\u00F6rhead"),
    ];

    [TestCaseSource(nameof(LiteralNoncharacterCases))]
    public void NoncharacterFFFE_IsComposedAround_InLiteralMode(string text, string prepared) =>
        Assert.That(Normalization.Prepare(text, ComparisonMode.Literal), Is.EqualTo(prepared));

    [Test]
    public void EveryCodeUnit_AloneAndInContext()
    {
        string[] contexts = ["{0}", "a{0}", "{0}\u0301", "\uD800{0}", "\u0435{0}\u0308", "{0}\uFFFE"];
        for (int c = 0; c <= char.MaxValue; c++)
            foreach (var context in contexts)
                AssertTotal(string.Format(context, (char)c));
    }

    [Test]
    public void GeneratedMalformedText()
    {
        // Strings drawn from pools weighted towards what breaks normalizers: lone and reversed
        // surrogates, noncharacters, combining marks in long runs, default-ignorable characters,
        // Hangul jamo, U+0345 (a mark that case folds to a letter), and characters with very long
        // compatibility decompositions.
        string[][] pools =
        [
            ["a", "Z", " ", "e", "\u0435", "\u0415", "o", "\u03A3", "\u0131", "\u0130", "\u00DF", "\u1E9E", "\u00D8"],
            ["\uD800", "\uDBFF", "\uDC00", "\uDFFF", "\uD83D", "\uDE00"],
            ["\uFFFE", "\uFFFF", "\uFFFD", "\uFDD0", "\U0001FFFE", "\U0010FFFF"],
            ["\u0301", "\u0308", "\u0323", "\u0345", "\u093E", "\u0E49", "\u05B8", "\u3099", "\u20DD", "\U0001D165"],
            ["\u00AD", "\u034F", "\u200D", "\uFE0F", "\u115F", "\uFFA0", "\U000E0001", "\U000E0100"],
            ["\u1100", "\u1161", "\u11A8", "\uAC00", "\u3131", "\uFFA1"],
            ["\uFDFA", "\u3300", "\uFB01", "\u2474", "\uFF76", "\uFF9E", "\u00BD"],
            ["\U00010400", "\U0001F600", "\U0002F800", "\u4E00", "\u0915", "\u05E9"],
        ];
        var random = new Random(20261005);
        for (int n = 0; n < 20_000; n++)
        {
            var text = new StringBuilder();
            int length = random.Next(0, 24);
            for (int i = 0; i < length; i++)
            {
                var pool = pools[random.Next(pools.Length)];
                text.Append(pool[random.Next(pool.Length)]);
            }

            AssertTotal(text.ToString());
        }
    }

    private static void AssertTotal(string text)
    {
        foreach (var mode in Modes)
        {
            string prepared = "";
            Assert.DoesNotThrow(() => prepared = Normalization.Prepare(text, mode), $"{mode}: {Describe(text)}");
            if (!IsWellFormed(prepared) || !IsNfc(prepared))
                Assert.Fail($"{mode}: {Describe(text)} became {Describe(prepared)}, which is not well-formed NFC");
        }
    }

    private static bool IsWellFormed(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            else if (char.IsSurrogate(text[i])) return false;
        }

        return true;
    }

    // .NET's check throws on U+FFFE, around which no normalization form acts, so the pieces are checked.
    private static bool IsNfc(string text) => text.Split('\uFFFE').All(piece => piece.IsNormalized(NormalizationForm.FormC));

    private static string Describe(string text) => string.Join(" ", text.Select(c => $"{(int)c:X4}"));
}
