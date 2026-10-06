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
    [TestCase("FALLBACK(x, 1 AS NUMBER) > 1", new string[0])]
    [TestCase("FALLBACK(x, \"5\" AS NUMBER) > 1", new string[0])]
    [TestCase("FALLBACK(x, \"y\" AS NUMBER) > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("FALLBACK(x, COUNT(y)) > 1", new[] { Codes.FallbackDefaultNotConstant })]
    [TestCase("FALLBACK(artist, 5 AS STRING) == \"5\"", new string[0])]
    [TestCase("\"abc\" AS NUMBER > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("\"12\" AS NUMBER > 1", new string[0])]
    [TestCase("\"abc\" AS STRING AS NUMBER > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("\"abc\" AS NUMBER AS NUMBER > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("TRUE AS NUMBER > 1", new string[0])]
    [TestCase("\"1kb\" AS STRING AS NUMBER > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("1kb AS STRING AS NUMBER > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("1.5 AS DURATION > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("-1 AS BYTECOUNT > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("\"x\" AS DURATION > 1", new[] { Codes.ConstantDoesNotConvert })]
    [TestCase("\"245\" AS DURATION > 1", new string[0])]
    [TestCase("artist BETWEEN \"b\"..\"a\"", new[] { Codes.RangeReversed })]
    [TestCase("file::size BETWEEN 5..1", new[] { Codes.RangeReversed })]
    public void Rules(string text, string[] codes)
    {
        Assert.That(Constants.Analyse(Analyse.Bind(text)).Diagnostics.Codes(), Is.EqualTo(codes));
    }

    [TestCase("\"5\" AS NUMBER > 1", "\"5\" AS NUMBER", "5")]
    [TestCase("1.50 AS STRING == \"x\"", "1.50 AS STRING", "1.5")]
    [TestCase("1kb AS NUMBER AS STRING == \"x\"", "1kb AS NUMBER AS STRING", "1000")]
    [TestCase("1kb AS STRING == \"x\"", "1kb AS STRING", "1000b")]
    [TestCase("4m5s AS STRING == \"x\"", "4m5s AS STRING", "245s")]
    [TestCase("\"4:05\" AS DURATION > 1", "\"4:05\" AS DURATION", "245")]
    [TestCase("\"10kib\" AS BYTECOUNT > 1", "\"10kib\" AS BYTECOUNT", "10240")]
    [TestCase("245 AS DURATION > 1", "245 AS DURATION", "245")]
    [TestCase("file::duration AS STRING == \"x\"", "file::duration AS STRING", null)]
    public void AConversionOfAConstant_IsWorkedOutWhenThePredicateIsRead(string text, string written, string? value)
    {
        var tree = Analyse.Bind(text);

        var folded = Constants.Analyse(tree).ValueOf(tree.Find(written));

        Assert.That(folded?.Apply(new LiteralText()), Is.EqualTo(value));
    }

    private sealed class LiteralText : Goro.Predicates.Evaluation.IExpressionFunc<string?>
    {
        public string? Invoke<T>(Goro.Predicates.Evaluation.Expression<T> expression) where T : notnull =>
            expression is Goro.Predicates.Evaluation.Literal<T> literal
                ? literal.Value switch
                {
                    Goro.Predicates.Values.Duration duration => $"{duration.Seconds}",
                    Goro.Predicates.Values.ByteCount bytes => $"{bytes.Bytes}",
                    var value => $"{value}",
                }
                : null;
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
