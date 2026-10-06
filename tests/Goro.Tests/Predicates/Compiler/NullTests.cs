using Goro.Messages;
using static Goro.Tests.Predicates.Support.CompileAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary><c>NULL</c> names nothing; written where SQL would write it, it is answered with the state test meant.</summary>
public class NullTests
{
    [TestCase("year == NULL", "year IS ABSENT")]
    [TestCase("year == null", "year IS ABSENT")]
    [TestCase("NULL == year", "year IS ABSENT")]
    [TestCase("year == (NULL)", "year IS ABSENT")]
    [TestCase("ALL(year) == NULL", "year IS ABSENT")]
    [TestCase("year != NULL", "NOT year IS ABSENT")]
    [TestCase("x > 1 AND genre != NULL", "x > 1 AND NOT genre IS ABSENT")]
    public void ComparisonWithNull_OffersTheStateTest(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.NullValue));
        Assert.That(Marked(text, error), Is.EqualTo(text.Contains("null") ? "null" : "NULL"));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
        Compiles(rewrite);
    }

    [TestCase("year < NULL")]
    [TestCase("NULL IS ABSENT")]
    [TestCase("COUNT(NULL) > 1")]
    [TestCase("FALLBACK(year, NULL) > 1")]
    [TestCase("NULL")]
    [TestCase("year BETWEEN 1..2 OR NULL")]
    public void NullAnywhereElse_SaysThereIsNoNull(string text)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.NullValue));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.NullValue()));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [Test]
    public void NullOnBothSides_IsTwoErrors()
    {
        Assert.That(Codes("NULL == NULL"), Is.EqualTo(new[] { Codes.NullValue, Codes.NullValue }));
    }

    [Test]
    public void NullComparison_StillChecksItsOtherOperand()
    {
        Assert.That(Codes("yaer == NULL"), Is.EqualTo(new[] { Codes.UnknownIdentifier, Codes.NullValue }));
    }
}
