using Goro.Messages;
using static Goro.Tests.Predicates.Support.CompileAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>
/// Being exactly one, where it matters: the predicate and the operands of the logical operators must be
/// exactly one boolean, and an operand of <c>!=</c> that is not exactly one must have its quantifier written.
/// </summary>
public class ExactlyOneTests
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
    public void BooleanNotExactlyOne_WhereAConditionIsWanted_OffersBothComparisons(string text, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.ConditionNotExactlyOne));
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
    public void BooleansThatAreExactlyOne_AreConditions(string text)
    {
        Compiles(text);
    }

    // The testing document's row of four errors: LITERALLY is not a quantifier, FALLBACK settles
    // absence but not several occurrences, and every operand that is not exactly one needs a quantifier of
    // its own. Where one that needs it can hold several, the quantified readings are what is offered.
    [TestCase("genre != \"x\"", "NOT genre == \"x\"", "ALL(genre) != \"x\"")]
    [TestCase("LITERALLY(genre) != \"x\"", "NOT LITERALLY(genre) == \"x\"", "ALL(LITERALLY(genre)) != \"x\"")]
    [TestCase("FALLBACK(genre, \"\") != \"x\"", "NOT FALLBACK(genre, \"\") == \"x\"", "ALL(FALLBACK(genre, \"\")) != \"x\"")]
    [TestCase("ALL(a) != b", "NOT ALL(a) == b", "ALL(a) != ALL(b)")]
    [TestCase("genre != id3v1::genre", "NOT genre == id3v1::genre", "ALL(genre) != ALL(id3v1::genre)")]
    [TestCase("\"x\" != genre", "NOT \"x\" == genre", "\"x\" != ALL(genre)")]
    [TestCase("genre != artist", "NOT genre == artist", "ALL(genre) != ALL(artist)")]
    public void NotEqual_WithAnOperandNotExactlyOneAndNoQuantifier_OffersBothReadings(string text, string negated, string universal)
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

    // A quantifier anywhere in a stack of modifiers satisfies the rule, and an operand that is exactly one needs none.
    [TestCase("ANY(genre) != \"x\"")]
    [TestCase("LITERALLY(ALL(genre)) != \"x\"")]
    [TestCase("COUNT(genre) != 1")]
    [TestCase("file::size != 0")]
    [TestCase("(a == b) != (c == d)")]
    [TestCase("one != \"x\"")]
    [TestCase("ALL(a) != ANY(b)")]
    [TestCase("(ALL(genre)) != \"x\"")]
    public void NotEqual_WithEveryOperandExactlyOneOrQuantified_IsValid(string text)
    {
        Compiles(text);
    }

    // An operand that can be absent but never holds several: a quantifier has nothing to choose
    // among, so the diagnostic is about absence, and offers NOT and a FALLBACK() naming its default.
    [TestCase("file::extension != \"flac\"", "NOT file::extension == \"flac\"", "FALLBACK(file::extension, \"\") != \"flac\"")]
    [TestCase("id3v1::year != 1991", "NOT id3v1::year == 1991", "FALLBACK(id3v1::year, 0) != 1991")]
    [TestCase("\"x\" != id3v1::genre", "NOT \"x\" == id3v1::genre", "\"x\" != FALLBACK(id3v1::genre, \"\")")]
    [TestCase("LITERALLY(id3v1::genre) != \"x\"", "NOT LITERALLY(id3v1::genre) == \"x\"", "LITERALLY(FALLBACK(id3v1::genre, \"\")) != \"x\"")]
    [TestCase("id3v1::genre != id3v1::artist", "NOT id3v1::genre == id3v1::artist", "FALLBACK(id3v1::genre, \"\") != FALLBACK(id3v1::artist, \"\")")]
    public void NotEqual_WithAnOperandThatCanOnlyBeAbsent_SaysSo_AndOffersWaysToDecideAbsence(string text, string negated, string settled)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.AmbiguousNotEqual));
        Assert.That(error.Message, Is.InstanceOf<ErrorMessage.MayBeAbsentNotEqual>().Or.InstanceOf<ErrorMessage.MayBeAbsentNotEqualBoth>());
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { negated, settled }));
        Assert.Multiple(() =>
        {
            foreach (var rewrite in Rewrites(text, error))
            {
                Compiles(rewrite);
            }
        });
    }

    [Test]
    public void NotEqual_TheDefaultOffered_IsTheEmptyValueOfTheType()
    {
        Assert.That(Rewrites("file::duration != 3m AND file::extension != \"x\"", Error("file::duration != 3m AND file::extension != \"x\"")),
            Has.Some.Contains("FALLBACK(file::extension, \"\")"));
        Assert.That(Rewrites("id3v1::track != 3", Error("id3v1::track != 3"))[1], Is.EqualTo("FALLBACK(id3v1::track, 0) != 3"));
    }

    [TestCase("FALLBACK(file::extension, \"\") != \"mp3\"")]
    [TestCase("FALLBACK(id3v1::genre, \"\") != \"blues\"")]
    [TestCase("PREFERRED(id3v1::genre, \"x\") != \"y\"")]
    [TestCase("ANY(id3v1::genre) != \"blues\"")]
    [TestCase("file::duration != 3m")]
    public void NotEqual_OnAnOperandThatIsExactlyOne_OrQuantified_IsValid(string text)
    {
        Compiles(text);
    }
}
