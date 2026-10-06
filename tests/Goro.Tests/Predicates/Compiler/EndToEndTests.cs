using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;
using static Goro.Tests.Predicates.Support.CompileAssert;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>
/// Predicates from text to a truth: lexed, parsed, bound by the real compiler and evaluated over a
/// catalog of canned values. The regex and normalization rows of the testing document, and a few
/// quantifier rows as a smoke test of everything joined up.
/// </summary>
public class EndToEndTests
{
    private static Truth Decide(string text, string artist) =>
        Evaluate(text, TestCatalog.Standard().With("artist", Ok(artist))).Truth;

    // The subject is prepared, the pattern is not, and the match ignores case unless LITERALLY says otherwise.
    [TestCase("artist =~ r\"^mot\"", "Motörhead", Truth.True)]
    [TestCase("artist =~ r\"^MOT\"", "Motörhead", Truth.True)]
    [TestCase("artist =~ r\"motö\"", "Motörhead", Truth.False)]
    [TestCase("LITERALLY(artist) =~ r\"otö\"", "Motörhead", Truth.True)]
    [TestCase("LITERALLY(artist) =~ r\"^mot\"", "Motörhead", Truth.False)]
    [TestCase("artist =~ r\"소년\"", "방탄소년단", Truth.True)]
    [TestCase("artist =~ r\"ガン\"", "ガンダム", Truth.True)]
    [TestCase("artist =~ r\"strasse\"", "Straße", Truth.True)]
    [TestCase("artist =~ r\"straße\"", "Straße", Truth.False)]
    [TestCase("artist =~ r\"^met\"", "Metallica", Truth.True)]
    [TestCase("LITERALLY(artist) =~ r\"^met\"", "Metallica", Truth.False)]
    [TestCase("LITERALLY(artist) =~ r\"\\p{Lu}\"", "Metallica", Truth.True)]
    [TestCase(@"artist =~ r""^\d+ """, "10 Years", Truth.True)]
    public void Match_NormalizesTheSubjectAndNotThePattern(string text, string artist, Truth expected)
    {
        Assert.That(Decide(text, artist), Is.EqualTo(expected));
    }

    // A string endpoint is compared with only as many characters of the subject as it has, so a
    // range reaches every string beginning with its maximum, as the spine of an encyclopedia does.
    [TestCase("artist BETWEEN \"ma\"..\"mi\"", "Miles Davis", Truth.True)]
    [TestCase("artist BETWEEN \"ma\"..\"mi\"", "Metallica", Truth.True)]
    [TestCase("artist BETWEEN \"ma\"..\"mi\"", "Motörhead", Truth.False)]
    [TestCase("artist BETWEEN \"ma\"..\"mi\"", "M", Truth.False)]
    [TestCase("artist BETWEEN \"a\"..\"b\"", "Blues Traveler", Truth.True)]
    [TestCase("artist BETWEEN \"m\"..\"n\"", "Nirvana", Truth.True)]
    [TestCase("artist BETWEEN \"the\"..\"the\"", "The Beatles", Truth.True)]
    [TestCase("artist BETWEEN \"the\"..\"the\"", "Th", Truth.False)]
    [TestCase("artist BETWEEN \"mi\"..\"m\"", "Miles Davis", Truth.True)]
    [TestCase("artist BETWEEN \"mi\"..\"m\"", "Madonna", Truth.False)]
    [TestCase("LITERALLY(artist) BETWEEN \"M\"..\"Mi\"", "Miles Davis", Truth.True)]
    [TestCase("LITERALLY(artist) BETWEEN \"M\"..\"Mi\"", "miles davis", Truth.False)]
    [TestCase("artist BETWEEN \"st\"..\"strasse\"", "Straße", Truth.True)]
    public void Range_OfStrings_TreatsItsEndpointsAsPrefixes(string text, string artist, Truth expected)
    {
        Assert.That(Decide(text, artist), Is.EqualTo(expected));
    }

    // e, an optional combining acute, end. Removing the mark from the pattern, as normalization
    // would, leaves ^cafe?$, an optional e, which matches "caf".
    [TestCase("Cafe", Truth.True)]
    [TestCase("Café", Truth.True)]
    [TestCase("caf", Truth.False)]
    public void Pattern_ThatWouldChangeMeaningIfDecomposed_IsMatchedAsWritten(string artist, Truth expected)
    {
        Assert.That(Decide("artist =~ r\"^cafe\u0301?$\"", artist), Is.EqualTo(expected));
    }

    // normalization.md: in normalized mode the match ignores case, so case classes match a letter
    // of either case; a pattern that tells case apart works only in literal mode.
    [TestCase("artist =~ r\"\\p{Lu}\"", "metallica", Truth.True)]
    [TestCase("artist =~ r\"[A-Z]\"", "metallica", Truth.True)]
    [TestCase("LITERALLY(artist) =~ r\"\\p{Lu}\"", "metallica", Truth.False)]
    [TestCase("LITERALLY(artist) =~ r\"\\p{Lu}\"", "Metallica", Truth.True)]
    public void CaseClasses_IgnoreCaseUnlessLiterally(string text, string artist, Truth expected)
    {
        Assert.That(Decide(text, artist), Is.EqualTo(expected));
    }

    [TestCase("artist == \"motorhead\"", "Motörhead", Truth.True)]
    [TestCase("LITERALLY(artist) == \"motorhead\"", "Motörhead", Truth.False)]
    [TestCase("artist == LITERALLY(\"Motörhead\")", "Motörhead", Truth.True)]
    [TestCase("artist == \"orsted\"", "Ørsted", Truth.True)]
    [TestCase("artist == \"ymo\"", "ＹＭＯ", Truth.True)]
    [TestCase("artist BETWEEN \"ma\"..\"mi\"", "Metallica", Truth.True)]
    [TestCase("LITERALLY(artist) BETWEEN \"m\"..\"n\"", "Metallica", Truth.False)]
    [TestCase("artist BETWEEN \"MA\"..\"MI\"", "metallica", Truth.True)]
    [TestCase("\"a\\\\b\" == r\"a\\b\"", "any", Truth.True)]
    public void Comparison_NormalizesUnlessLiterally(string text, string artist, Truth expected)
    {
        Assert.That(Decide(text, artist), Is.EqualTo(expected));
    }

    private static TestCatalog Bags() => TestCatalog.Standard()
        .With("a", Ok(1m), Ok(2m))
        .With("b", Ok(1m), Ok(2m), Ok(3m))
        .With("v", Ok(0m), Ok(20m))
        .With("w", Ok(1m), BadNumber)
        .With("genre", Ok("metal"), Ok("rock"));

    [TestCase("ALL(a) == b", Truth.True)]
    [TestCase("b == ALL(a)", Truth.True)]
    [TestCase("ALL(b) == a", Truth.False)]
    [TestCase("a == ALL(b)", Truth.False)]
    [TestCase("ALL(a) < b", Truth.True)]
    [TestCase("ALL(a) < ALL(b)", Truth.False)]
    [TestCase("v BETWEEN 1..10", Truth.False)]
    [TestCase("1 <= v AND v <= 10", Truth.True)]
    [TestCase("ALL(x) == 1", Truth.False)]
    [TestCase("ALL(x) IS USABLE", Truth.False)]
    [TestCase("x IS ABSENT", Truth.True)]
    [TestCase("ANY(x) != 1", Truth.False)]
    [TestCase("ALL(x) != 1", Truth.False)]
    [TestCase("NOT x == 1", Truth.True)]
    [TestCase("w == 1", Truth.True)]
    [TestCase("ALL(w) == 1", Truth.Unusable)]
    [TestCase("ALL(w) == 2", Truth.False)]
    [TestCase("genre == \"metal\"", Truth.True)]
    [TestCase("ANY(genre) != \"metal\"", Truth.True)]
    [TestCase("ALL(genre) != \"metal\"", Truth.False)]
    [TestCase("NOT genre == \"metal\"", Truth.False)]
    [TestCase("COUNT(genre) > 1", Truth.True)]
    [TestCase("COUNT(x) == 0", Truth.True)]
    [TestCase("FALLBACK(year, (0)) == 0", Truth.True)]
    [TestCase("STRING(1.50) == \"1.5\"", Truth.True)]
    [TestCase("NUMBER(\".5\") == 0.5", Truth.True)]
    public void Quantifiers_SmokeTest(string text, Truth expected)
    {
        Assert.That(Evaluate(text, Bags()).Truth, Is.EqualTo(expected));
    }

    [Test]
    public void UnusableOccurrence_WarnsEvenWhenAnotherSettledTheOperator()
    {
        var outcome = Evaluate("w == 1", Bags());

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "w" }));
    }

    [Test]
    public void Guard_ShortCircuitsBeforeTheWarning()
    {
        var catalog = Bags();

        Assert.That(Evaluate("ALL(w) IS USABLE AND w > 0", catalog).Reported, Is.Empty);
        Assert.That(Evaluate("w > 0 AND ALL(w) IS USABLE", catalog).Reported, Has.Count.EqualTo(1));
    }
}
