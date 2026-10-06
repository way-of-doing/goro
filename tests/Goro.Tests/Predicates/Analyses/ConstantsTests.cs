using Goro.Predicates.Analyses;
using Goro.Predicates.Binding;
using Goro.Tests.Predicates.Support;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Analyses;

/// <summary>Literal values and the rules about them, and the prepared ends of every range.</summary>
public class ConstantsTests
{
    [TestCase("file::size > -1", new[] { Codes.NegativeUnitLiteral })]
    [TestCase("file::duration > -1", new[] { Codes.NegativeUnitLiteral })]
    [TestCase("file::duration > 1.5", new[] { Codes.FractionalDurationLiteral })]
    [TestCase("file::size > 1.5", new string[0])]
    [TestCase("x > -1.5", new string[0])]
    [TestCase("FALLBACK(file::size, -1) > 1", new[] { Codes.NegativeUnitLiteral })]
    [TestCase("FALLBACK(x, y) > 1", new[] { Codes.FallbackDefaultNotConstant })]
    [TestCase("FALLBACK(x, (1)) > 1", new string[0])]
    [TestCase("FALLBACK(x, NUMBER(1)) > 1", new string[0])]
    [TestCase("FALLBACK(x, NUMBER(\"5\")) > 1", new string[0])]
    [TestCase("FALLBACK(x, NUMBER(\"y\")) > 1", new[] { Codes.InvalidNumberLiteral })]
    [TestCase("FALLBACK(x, COUNT(y)) > 1", new[] { Codes.FallbackDefaultNotConstant })]
    [TestCase("FALLBACK(artist, STRING(5)) == \"5\"", new string[0])]
    [TestCase("NUMBER(\"abc\") > 1", new[] { Codes.InvalidNumberLiteral })]
    [TestCase("NUMBER(\"12\") > 1", new string[0])]
    [TestCase("NUMBER(STRING(\"abc\")) > 1", new[] { Codes.InvalidNumberLiteral })]
    [TestCase("NUMBER(NUMBER(\"abc\")) > 1", new[] { Codes.InvalidNumberLiteral })]
    [TestCase("NUMBER(TRUE) > 1", new string[0])]
    [TestCase("artist BETWEEN \"b\"..\"a\"", new[] { Codes.RangeReversed })]
    [TestCase("file::size BETWEEN 5..1", new[] { Codes.RangeReversed })]
    public void Rules(string text, string[] codes)
    {
        Assert.That(Constants.Analyse(Analyse.Bind(text)).Diagnostics.Codes(), Is.EqualTo(codes));
    }

    [TestCase("NUMBER(\"5\") > 1", "NUMBER(\"5\")", "5")]
    [TestCase("STRING(1.50) == \"x\"", "STRING(1.50)", "1.5")]
    [TestCase("NUMBER(STRING(1kb)) > 1", "NUMBER(STRING(1kb))", "1000")]
    [TestCase("STRING(file::duration) == \"x\"", "STRING(file::duration)", null)]
    public void AConversionOfAConstant_IsWorkedOutWhenThePredicateIsRead(string text, string written, string? value)
    {
        var tree = Analyse.Bind(text);

        var folded = Constants.Analyse(tree).ValueOf(tree.Find(written));

        Assert.That(folded?.Apply(new LiteralText()), Is.EqualTo(value));
    }

    private sealed class LiteralText : Goro.Predicates.Evaluation.IExpressionFunc<string?>
    {
        public string? Invoke<T>(Goro.Predicates.Evaluation.Expression<T> expression) where T : notnull =>
            expression is Goro.Predicates.Evaluation.Literal<T> literal ? $"{literal.Value}" : null;
    }

    [TestCase("artist BETWEEN \"B\"..\"a\"", true)]
    [TestCase("LITERALLY(artist) BETWEEN \"B\"..\"a\"", false)]
    public void Reversal_IsJudgedInTheOperatorsMode(string text, bool reversed)
    {
        var tree = Analyse.Bind(text);

        var analysis = Constants.Analyse(tree);

        Assert.That(analysis.Diagnostics.Codes(), Is.EqualTo(reversed ? new[] { Codes.RangeReversed } : []));
        Assert.That(analysis.RangeOf((SemanticRange)tree.Root), reversed ? Is.Null : Is.Not.Null);
    }

    [Test]
    public void AnEndThatCannotStandForItsUnit_IsReportedAlone()
    {
        var tree = Analyse.Bind("file::size BETWEEN 5..-1");

        var analysis = Constants.Analyse(tree);

        Assert.That(analysis.Diagnostics.Codes(), Is.EqualTo(new[] { Codes.NegativeUnitLiteral }));
        Assert.That(analysis.RangeOf((SemanticRange)tree.Root), Is.Null);
    }

    [TestCase("x BETWEEN 1..\"a\"")]
    [TestCase("flag BETWEEN TRUE..FALSE")]
    public void EndsTheBinderRejected_AreNotCompared(string text)
    {
        var tree = Analyse.Bind(text);

        Assert.That(tree.Diagnostics, Is.Not.Empty);
        Assert.That(Constants.Analyse(tree).Diagnostics, Is.Empty);
    }

    [Test]
    public void ATreeWithErrors_StillHasItsOwnChecked()
    {
        var tree = Analyse.Bind("artst == 1 AND file::size > -1");

        Assert.That(tree.Diagnostics.Codes(), Is.EqualTo(new[] { Codes.UnknownIdentifier }));
        Assert.That(Constants.Analyse(tree).Diagnostics.Codes(), Is.EqualTo(new[] { Codes.NegativeUnitLiteral }));
    }
}
