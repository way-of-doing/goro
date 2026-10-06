using static Goro.Tests.Predicates.Binding.Support.BindAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Binding;

/// <summary>
/// Definiteness, where it matters: the predicate and the operands of the logical operators must be
/// definite booleans, and an operand of <c>!=</c> that is not definite must have its quantifier written.
/// </summary>
public class DefinitenessBindingTests
{
    [TestCase("artist", "artist")]
    [TestCase("COUNT(genre)", "COUNT(genre)")]
    [TestCase("NOT year", "year")]
    [TestCase("x > 1 OR \"a\"", "\"a\"")]
    public void NonBoolean_WhereAConditionIsWanted_IsAnError(string text, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.NotACondition));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
    }

    [Test]
    public void BothOperandsOfAnd_AreCheckedIndependently()
    {
        const string text = "5 AND x";

        var errors = Errors(text);

        Assert.That(errors.Select(e => e.Code), Is.EqualTo(new[] { Codes.NotACondition, Codes.NotACondition }));
        Assert.That(errors.Select(e => Marked(text, e)), Is.EqualTo(new[] { "5", "x" }));
    }

    [TestCase("flag", "flag")]
    [TestCase("NOT flag", "flag")]
    [TestCase("flag AND x > 1", "flag")]
    [TestCase("FALLBACK(flag, TRUE)", "FALLBACK(flag, TRUE)")]
    public void IndefiniteBoolean_WhereAConditionIsWanted_OffersBothComparisons(string text, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.IndefiniteCondition));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[]
        {
            text.Replace(marked, marked + " == TRUE"),
            text.Replace(marked, marked + " == FALSE"),
        }));
    }

    [TestCase("TRUE")]
    [TestCase("false")]
    [TestCase("flag == TRUE")]
    [TestCase("NOT NOT x > 1")]
    [TestCase("FALLBACK(x > 1, TRUE)")]
    [TestCase("(x > 1) AND (TRUE)")]
    public void DefiniteBooleans_AreConditions(string text)
    {
        Compiles(text);
    }

    // The testing document's row of five errors: LITERALLY is not a quantifier, a function of an
    // identifier is no more definite than the identifier, every operand that is not definite needs a
    // quantifier of its own, and file::extension can be absent.
    [TestCase("genre != \"x\"", "NOT genre == \"x\"", "ALL(genre) != \"x\"")]
    [TestCase("LITERALLY(genre) != \"x\"", "NOT LITERALLY(genre) == \"x\"", "ALL(LITERALLY(genre)) != \"x\"")]
    [TestCase("FALLBACK(genre, \"\") != \"x\"", "NOT FALLBACK(genre, \"\") == \"x\"", "ALL(FALLBACK(genre, \"\")) != \"x\"")]
    [TestCase("ALL(a) != b", "NOT ALL(a) == b", "ALL(a) != ALL(b)")]
    [TestCase("file::extension != \"flac\"", "NOT file::extension == \"flac\"", "ALL(file::extension) != \"flac\"")]
    [TestCase("\"x\" != genre", "NOT \"x\" == genre", "\"x\" != ALL(genre)")]
    [TestCase("genre != artist", "NOT genre == artist", "ALL(genre) != ALL(artist)")]
    public void NotEqual_WithAnIndefiniteOperandAndNoQuantifier_OffersBothReadings(string text, string negated, string universal)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.AmbiguousNotEqual));
        Assert.That(Marked(text, error), Is.EqualTo("!="));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { negated, universal }));
    }

    [Test]
    public void NotEqual_RewritesStandWhereverTheComparisonDoes()
    {
        const string text = "x > 1 AND (genre != \"x\")";

        Assert.That(Rewrites(text, Error(text)), Is.EqualTo(new[]
        {
            "x > 1 AND (NOT genre == \"x\")",
            "x > 1 AND (ALL(genre) != \"x\")",
        }));
    }

    [Test]
    public void NotEqual_RewritesAreValidPredicates()
    {
        const string text = "genre != \"x\"";

        foreach (var rewrite in Rewrites(text, Error(text)))
        {
            Compiles(rewrite);
        }
    }

    // A quantifier anywhere in a stack of modifiers satisfies the rule, and a definite operand needs none.
    [TestCase("ANY(genre) != \"x\"")]
    [TestCase("LITERALLY(ALL(genre)) != \"x\"")]
    [TestCase("COUNT(genre) != 1")]
    [TestCase("file::size != 0")]
    [TestCase("(a == b) != (c == d)")]
    [TestCase("one != \"x\"")]
    [TestCase("ALL(a) != ANY(b)")]
    [TestCase("(ALL(genre)) != \"x\"")]
    public void NotEqual_WithEveryOperandDefiniteOrQuantified_IsValid(string text)
    {
        Compiles(text);
    }
}
