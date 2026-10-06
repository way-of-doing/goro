using Goro.Messages;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;
using static Goro.Tests.Predicates.Support.CompileAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>
/// The conversion operator <c>x AS T</c>: which pairs of types convert, the rules every conversion
/// keeps however it was written, and what is offered for the spellings of other languages.
/// </summary>
public class ConversionTests
{
    // In the standard test catalog s is a string and x a number; file::size and file::duration are units.
    [TestCase("s AS NUMBER > 1", typeof(Conversion<string, decimal>))]
    [TestCase("s AS DURATION > 1m", typeof(Conversion<string, Duration>))]
    [TestCase("s AS BYTECOUNT > 1kb", typeof(Conversion<string, ByteCount>))]
    [TestCase("x AS STRING == \"1\"", typeof(Conversion<decimal, string>))]
    [TestCase("x AS DURATION > 1m", typeof(Conversion<decimal, Duration>))]
    [TestCase("x AS BYTECOUNT > 1kb", typeof(Conversion<decimal, ByteCount>))]
    [TestCase("file::duration AS NUMBER > 1", typeof(Conversion<Duration, decimal>))]
    [TestCase("file::duration AS STRING == \"1s\"", typeof(Conversion<Duration, string>))]
    [TestCase("file::size AS NUMBER > 1", typeof(Conversion<ByteCount, decimal>))]
    [TestCase("file::size AS STRING == \"1b\"", typeof(Conversion<ByteCount, string>))]
    [TestCase("s AS duration > 1m", typeof(Conversion<string, Duration>))]
    public void EachPairThatConverts_IsAConversion(string text, Type node)
    {
        Assert.That(Compiles(text).Root.LeftOperand(), Is.TypeOf(node));
    }

    [TestCase("x AS NUMBER > 1")]
    [TestCase("s AS STRING == \"a\"")]
    [TestCase("file::duration AS DURATION > 1m")]
    public void AConversionToItsOwnType_IsItsOperand(string text)
    {
        Assert.That(Compiles(text).Root.LeftOperand().GetType().GetGenericTypeDefinition(), Is.EqualTo(typeof(IdentifierReference<>)));
    }

    [TestCase("file::duration AS BYTECOUNT > 1kb", "file::duration")]
    [TestCase("file::size AS DURATION > 1m", "file::size")]
    public void ADurationAndABytecount_DoNotConvertIntoEachOther(string text, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.UnitsDoNotConvert));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
    }

    [TestCase("s AS NUMBR > 1", "s AS NUMBER > 1")]
    [TestCase("s AS numbr > 1", "s AS number > 1")]
    [TestCase("s AS DURATON > 1m", "s AS DURATION > 1m")]
    public void AnUnknownTarget_OffersTheClosest(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.UnknownTarget));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void AnUnknownTarget_FarFromEveryTarget_OffersNothing_AndIsReportedOnce()
    {
        Assert.That(Error("s AS BOOLEAN > 1").Suggestions, Is.Empty);
    }

    [TestCase("ALL(s) AS NUMBER > 1", "ALL", "ALL(s AS NUMBER) > 1")]
    [TestCase("LITERALLY(ALL(s)) AS STRING == \"a\"", "LITERALLY", "LITERALLY(ALL(s AS STRING)) == \"a\"")]
    [TestCase("(ANY(s)) AS NUMBER != 1", "ANY", "(ANY(s AS NUMBER)) != 1")]
    [TestCase("ANY(id3v2::title) AS NUMBER != 1", "ANY", "ANY(id3v2::title AS NUMBER) != 1")]
    public void AModifierOnTheOperand_IsMisplaced_AndOffersItOutside(string text, string modifier, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.MisplacedModifier));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.MisplacedModifierInConversion(new Code(modifier))));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
        Compiles(rewrite);
    }

    // A misplaced modifier is taken as written outside the conversion, where its rewrite puts it, so
    // the rules that read an operand's modifiers see it there: one misplacement, one error. The range
    // is in order only in literal mode, so it shows the inherited LITERALLY being read.
    [TestCase("ALL(x) AS NUMBER AS STRING != \"1\"")]
    [TestCase("ALL(x AS NUMBER) AS STRING != \"1\"")]
    [TestCase("ALL(ALL(s) AS NUMBER) != 1")]
    [TestCase("LITERALLY(s) AS STRING BETWEEN \"B\"..\"a\"")]
    public void AMisplacedModifier_IsInheritedByTheOperand_SoItIsReportedOnce(string text)
    {
        Assert.That(Codes(text), Is.EqualTo(new[] { Codes.MisplacedModifier }));
    }

    // A second error only where the rewrite would itself be wrong.
    [TestCase("ALL(x) AS NUMBER IS ABSENT", Codes.QuantifierOnAbsentTest)]
    [TestCase("ANY(ALL(x) AS NUMBER) != 1", Codes.ContradictoryQuantifiers)]
    [TestCase("LITERALLY(x) AS NUMBER > 1", Codes.LiterallyNotString)]
    public void AMisplacedModifier_WhoseRewriteIsAlsoWrong_IsTwoErrors(string text, string second)
    {
        Assert.That(Codes(text), Is.EquivalentTo(new[] { Codes.MisplacedModifier, second }));
    }

    // The spellings of other languages: each one error, offering AS, and the rest of the predicate
    // checked as if that had been written.
    [TestCase("NUMBER(s) > 1", "s AS NUMBER > 1")]
    [TestCase("string(x) == \"1\"", "x AS string == \"1\"")]
    [TestCase("DURATION(s) > 1m", "s AS DURATION > 1m")]
    [TestCase("BYTECOUNT(s) > 1kb", "s AS BYTECOUNT > 1kb")]
    [TestCase("NUMBER(FALLBACK(s, \"0\")) > 1", "FALLBACK(s, \"0\") AS NUMBER > 1")]
    [TestCase("CAST(s AS NUMBER) > 1", "s AS NUMBER > 1")]
    [TestCase("x > 1 AND STRING(x) == \"1\"", "x > 1 AND x AS STRING == \"1\"")]
    public void AConversionSpelledAsAFunction_IsOneError_OfferingAs(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.ConversionCalled));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
        Compiles(rewrite);
    }

    [Test]
    public void AConversionSpelledAsAFunction_OfAnOperator_ParenthesizesItsArgument()
    {
        const string text = "NUMBER(x > 1) > 1";

        var errors = Errors(text);

        Assert.That(errors.Select(e => e.Code), Is.EqualTo(new[] { Codes.ConversionCalled, Codes.BooleanNotConvertible }));
        Assert.That(Rewrites(text, errors[0]), Is.EqualTo(new[] { "(x > 1) AS NUMBER > 1" }));
    }

    [Test]
    public void AConversionSpelledAsAFunction_IsTypedAsTheConversion_SoNothingElseIsReported()
    {
        Assert.That(Codes("NUMBER(s) > 1 AND DURATION(s) > 1m"), Is.EqualTo(new[] { Codes.ConversionCalled, Codes.ConversionCalled }));
    }

    [TestCase("NUMBER(s, s) > 1")]
    [TestCase("CAST(s) > 1")]
    public void AConversionNameWithoutOneArgument_IsAnUnknownFunction(string text)
    {
        Assert.That(Codes(text), Does.Contain(Codes.UnknownFunction));
    }

    [TestCase("\"4:05\" AS DURATION == 245", Truth.True)]
    [TestCase("\"4m5s\" AS DURATION == 4:05", Truth.True)]
    [TestCase("\"10kib\" AS BYTECOUNT == 10240", Truth.True)]
    [TestCase("4m5s AS STRING == \"245s\"", Truth.True)]
    [TestCase("1.4kib AS STRING == \"1433.6b\"", Truth.True)]
    [TestCase("1.50 AS STRING == \"1.5\"", Truth.True)]
    [TestCase("4m5s AS NUMBER AS STRING == \"245\"", Truth.True)]
    [TestCase("1.4kib AS STRING AS BYTECOUNT == 1.4kib", Truth.True)]
    public void Constants_ConvertWhenThePredicateIsRead(string text, Truth truth)
    {
        Assert.That(Evaluate(text, TestCatalog.Standard()).Truth, Is.EqualTo(truth));
    }

    // Every value reads back through AS STRING and its own type, by the literal grammar alone.
    [Test]
    public void EveryValue_SurvivesTheRoundTripThroughAsString()
    {
        var catalog = TestCatalog.Standard()
            .With("n", Ok(0m), Ok(-1.5m), Ok(1.50m), Ok(79228162514264337593543950335m))
            .With("d", Ok(new Duration(0)), Ok(new Duration(245)), Ok(new Duration(360000)))
            .With("b", Ok(new ByteCount(0)), Ok(new ByteCount(1433.6m)), Ok(new ByteCount(1_000_000_000_000m)));

        var outcome = Evaluate(
            "ALL(n AS STRING AS NUMBER) == n AND ALL(d AS STRING AS DURATION) == d AND ALL(b AS STRING AS BYTECOUNT) == b",
            catalog);

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
    }

    private static Usable<T> Ok<T>(T datum) where T : notnull => new(datum);
}

internal static class ComparisonTestExtensions
{
    /// <summary>The expression of a comparison's left operand, whatever its datum type.</summary>
    public static Expression LeftOperand(this Expression comparison) => comparison switch
    {
        ComparisonTest<decimal> c => c.Left.Expression,
        ComparisonTest<string> c => c.Left.Expression,
        ComparisonTest<Duration> c => c.Left.Expression,
        ComparisonTest<ByteCount> c => c.Left.Expression,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison)),
    };
}
