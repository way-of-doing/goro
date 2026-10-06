using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;
using static Goro.Tests.Predicates.Support.CompileAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>Functions: names, arity, argument types, and the literal-only default of <c>FALLBACK()</c>.</summary>
public class FunctionTests
{
    [TestCase("COUNT(genre) > 1")]
    [TestCase("count(genre) > 1")]
    [TestCase("Count(genre) > 1")]
    [TestCase("FALLBACK(year, 0) > 1")]
    [TestCase("number(s) > 1")]
    [TestCase("String(year) == \"1\"")]
    [TestCase("preferred(x, y) > 1")]
    [TestCase("Preferred(x, y) > 1")]
    public void FunctionNames_IgnoreCase(string text)
    {
        Compiles(text);
    }

    [TestCase("cuont(genre) > 1", "count(genre) > 1")]
    [TestCase("COUNTS(genre) > 1", "COUNT(genre) > 1")]
    [TestCase("NUMBR(s) > 1", "NUMBER(s) > 1")]
    [TestCase("FALLBAKC(year, 0) > 1", "FALLBACK(year, 0) > 1")]
    [TestCase("PREFERED(x, y) > 1", "PREFERRED(x, y) > 1")]
    public void UnknownFunction_OffersTheClosest(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.UnknownFunction));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void UnknownFunction_FarFromEveryFunction_OffersNothing()
    {
        Assert.That(Error("frobnicate(x) > 1").Suggestions, Is.Empty);
    }

    [Test]
    public void UnknownFunction_StillChecksItsArguments()
    {
        Assert.That(Codes("cuont(artst) > 1"), Is.EqualTo(new[] { Codes.UnknownFunction, Codes.UnknownIdentifier }));
    }

    [TestCase("COUNT() > 1")]
    [TestCase("COUNT(a, b) > 1")]
    [TestCase("NUMBER(s, t) > 1")]
    [TestCase("STRING() == \"x\"")]
    [TestCase("FALLBACK(year) > 1")]
    [TestCase("FALLBACK(year, 0, 1) > 1")]
    [TestCase("PREFERRED() > 1")]
    [TestCase("PREFERRED(x) > 1")]
    public void WrongNumberOfArguments_IsOneError(string text)
    {
        Assert.That(Codes(text), Is.EqualTo(new[] { Codes.WrongArgumentCount }));
    }

    // A literal in parentheses is a literal.
    [TestCase("FALLBACK(year, (0)) > 1990")]
    [TestCase("FALLBACK(year, ((0))) > 1990")]
    public void FallbackDefault_InParentheses_IsALiteral(string text)
    {
        var comparison = (ComparisonTest<decimal>)Compiles(text).Root;

        Assert.That(((Fallback<decimal>)comparison.Left.Expression).Default, Is.Zero);
    }

    [TestCase("FALLBACK(year, x) > 1")]
    [TestCase("FALLBACK(year, COUNT(genre)) > 1")]
    [TestCase("FALLBACK(x > 1, y > 1)")]
    public void FallbackDefault_ThatIsNotAConstant_IsAnError(string text)
    {
        Assert.That(Error(text).Code, Is.EqualTo(Codes.FallbackDefaultNotConstant));
    }

    [TestCase("FALLBACK(year, \"2000\") > 1", "FALLBACK(year, 2000) > 1")]
    [TestCase("FALLBACK(genre, 0) == \"x\"", null)]
    [TestCase("FALLBACK(x > 1, 0)", null)]
    public void FallbackDefault_OfAnotherType_IsAnError(string text, string? rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.TypeMismatch));
        Assert.That(Rewrites(text, error), Is.EqualTo(rewrite is null ? Array.Empty<string>() : new[] { rewrite }));
    }

    [Test]
    public void FallbackDefault_NumberLiteral_StandsForADuration()
    {
        var comparison = (ComparisonTest<Duration>)Compiles("FALLBACK(file::duration, 90) > 1m").Root;

        Assert.That(((Fallback<Duration>)comparison.Left.Expression).Default, Is.EqualTo(new Duration(90)));
    }

    [TestCase("FALLBACK(file::duration, 1.5) > 1m", Codes.FractionalDurationLiteral)]
    [TestCase("FALLBACK(file::size, -1) > 1", Codes.NegativeUnitLiteral)]
    public void FallbackDefault_NumberLiteral_ThatCannotStandIn_IsAnError(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    [Test]
    public void FallbackDefault_BehindAMisplacedModifier_IsOnlyMisplaced()
    {
        Assert.That(Codes("FALLBACK(year, ALL(0)) > 1"), Is.EqualTo(new[] { Codes.MisplacedModifier }));
    }

    [TestCase("FALLBACK(x > 1, FALSE)")]
    [TestCase("FALLBACK(genre, \"\") == \"x\"")]
    [TestCase("FALLBACK(genre, r\"\") == \"x\"")]
    public void Fallback_AcceptsAnyTypeWithAMatchingLiteral(string text)
    {
        Compiles(text);
    }

    // NUMBER(("x")) is the same static error as NUMBER("x").
    [TestCase("NUMBER(\"x\") > 1", "\"x\"")]
    [TestCase("NUMBER((\"x\")) > 1", "\"x\"")]
    [TestCase("NUMBER(r\"1.\") > 1", "r\"1.\"")]
    [TestCase("NUMBER(\" 5\") > 1", "\" 5\"")]
    [TestCase("NUMBER(\"1000000000000000000000000000000\") > 1", "\"1000000000000000000000000000000\"")]
    public void Number_OfAStringLiteralThatIsNotANumber_IsAnError(string text, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.InvalidNumberLiteral));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
    }

    [Test]
    public void Number_OfAValidStringLiteral_IsWorkedOutWhenThePredicateIsRead()
    {
        var comparison = (ComparisonTest<decimal>)Compiles("NUMBER(\"5\") > 1").Root;

        Assert.That(((Literal<decimal>)comparison.Left.Expression).Value, Is.EqualTo(5m));
    }

    [TestCase("NUMBER(year) > 1")]
    [TestCase("NUMBER(NUMBER(year)) > 1")]
    public void Number_OfANumber_IsItsArgument(string text)
    {
        var comparison = (ComparisonTest<decimal>)Compiles(text).Root;

        Assert.That(comparison.Left.Expression, Is.TypeOf<IdentifierReference<decimal>>());
    }

    [Test]
    public void String_OfAString_IsItsArgument()
    {
        var comparison = (ComparisonTest<string>)Compiles("STRING(artist) == \"x\"").Root;

        Assert.That(comparison.Left.Expression, Is.TypeOf<IdentifierReference<string>>());
    }

    [Test]
    [TestCase("FALLBACK(year, NUMBER(5)) > 1990", 5)]
    [TestCase("FALLBACK(year, NUMBER(\"5\")) > 1990", 5)]
    [TestCase("FALLBACK(year, NUMBER(STRING(5.0))) > 1990", 5)]
    public void FallbackDefault_ThatIsAConversionOfAConstant_IsWorkedOutWhenThePredicateIsRead(string text, decimal value)
    {
        var comparison = (ComparisonTest<decimal>)Compiles(text).Root;

        Assert.That(((Fallback<decimal>)comparison.Left.Expression).Default, Is.EqualTo(value));
    }

    [Test]
    public void FallbackDefault_OfAStringConstant()
    {
        var comparison = (ComparisonTest<string>)Compiles("FALLBACK(artist, STRING(5)) == \"5\"").Root;

        Assert.That(((Fallback<string>)comparison.Left.Expression).Default, Is.EqualTo("5"));
    }

    [Test]
    public void FallbackDefault_ThatIsAConversionThatFails_IsAnErrorWhenThePredicateIsRead()
    {
        Assert.That(Error("FALLBACK(year, NUMBER(\"x\")) > 1").Code, Is.EqualTo(Codes.InvalidNumberLiteral));
    }

    // Only a number literal stands for a bytecount, not every number constant.
    [Test]
    public void FallbackDefault_ThatIsANumberConstantButNotALiteral_DoesNotStandForABytecount()
    {
        Assert.That(Error("FALLBACK(file::size, NUMBER(\"5\")) > 1").Code, Is.EqualTo(Codes.TypeMismatch));
    }

    [TestCase("NUMBER(s) > 1", typeof(Conversion<string, decimal>))]
    [TestCase("NUMBER(file::size) > 1", typeof(Conversion<ByteCount, decimal>))]
    [TestCase("NUMBER(file::duration) > 1", typeof(Conversion<Duration, decimal>))]
    public void Number_ConvertsEachTypeItAccepts(string text, Type node)
    {
        Assert.That(((ComparisonTest<decimal>)Compiles(text).Root).Left.Expression, Is.TypeOf(node));
    }

    [TestCase("STRING(year) == \"1\"", typeof(Conversion<decimal, string>))]
    [TestCase("STRING(file::size) == \"1\"", typeof(Conversion<ByteCount, string>))]
    [TestCase("STRING(file::duration) == \"1\"", typeof(Conversion<Duration, string>))]
    public void String_ConvertsEachTypeItAccepts(string text, Type node)
    {
        Assert.That(((ComparisonTest<string>)Compiles(text).Root).Left.Expression, Is.TypeOf(node));
    }

    [TestCase("COUNT(genre) > 1")]
    [TestCase("COUNT(x < 1) > 1")]
    [TestCase("COUNT(file::size) > 1")]
    public void Count_AcceptsAnyType(string text)
    {
        Compiles(text);
    }

    [TestCase("PREFERRED(x, y) > 1", 2)]
    [TestCase("PREFERRED(x, y, a, b) > 1", 4)]
    [TestCase("PREFERRED(x, 0) > 1", 2)]
    public void Preferred_TakesTwoOrMoreArguments(string text, int count)
    {
        var comparison = (ComparisonTest<decimal>)Compiles(text).Root;

        Assert.That(((Preferred<decimal>)comparison.Left.Expression).Arguments, Has.Length.EqualTo(count));
    }

    [TestCase("PREFERRED(x, artist) > 1", "artist", null)]
    [TestCase("PREFERRED(x, \"2000\") > 1", "\"2000\"", "PREFERRED(x, 2000) > 1")]
    [TestCase("PREFERRED(\"a\", x) == \"b\"", "x", null)]
    [TestCase("PREFERRED(1, artist) == \"b\"", "1", null)]
    public void Preferred_AnArgumentOfAnotherType_IsAnErrorWhereItIsWritten(string text, string marked, string? rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.TypeMismatch));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
        Assert.That(Rewrites(text, error), Is.EqualTo(rewrite is null ? Array.Empty<string>() : new[] { rewrite }));
    }

    [Test]
    public void Preferred_EachArgumentOfAnotherType_IsAnErrorOfItsOwn()
    {
        Assert.That(Codes("PREFERRED(x, artist, y, genre) > 1"), Is.EqualTo(new[] { Codes.TypeMismatch, Codes.TypeMismatch }));
    }

    [TestCase("PREFERRED(file::size, 1000) > 1kb")]
    [TestCase("PREFERRED(1000, file::size) > 1kb")]
    public void Preferred_NumberLiteral_StandsForABytecount_WhereverItIsWritten(string text)
    {
        var comparison = (ComparisonTest<ByteCount>)Compiles(text).Root;

        var literal = ((Preferred<ByteCount>)comparison.Left.Expression).Arguments.OfType<Literal<ByteCount>>().Single();
        Assert.That(literal.Value, Is.EqualTo(new ByteCount(1000)));
    }

    [TestCase("PREFERRED(file::duration, 1.5) > 1m", Codes.FractionalDurationLiteral)]
    [TestCase("PREFERRED(-1, file::size) > 1", Codes.NegativeUnitLiteral)]
    public void Preferred_NumberLiteral_ThatCannotStandIn_IsAnError(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    [Test]
    public void Preferred_OnlyOfNumberLiterals_IsANumber()
    {
        Assert.That(((ComparisonTest<decimal>)Compiles("PREFERRED(1, 2) > 1").Root).Left.Expression, Is.TypeOf<Preferred<decimal>>());
    }

    [Test]
    public void Preferred_AModifierOnAnArgument_IsMisplaced()
    {
        Assert.That(Codes("PREFERRED(ALL(x), y) > 1"), Is.EqualTo(new[] { Codes.MisplacedModifier }));
    }

    [Test]
    public void Preferred_AnArgumentInError_IsReportedOnce_AndTheRestStillAgree()
    {
        Assert.That(Codes("PREFERRED(artst, y) > 1"), Is.EqualTo(new[] { Codes.UnknownIdentifier }));
    }

    [Test]
    public void Preferred_OfConditionsThatAreExactlyOne_IsAPredicate()
    {
        Compiles("PREFERRED(x > 1, y > 1, FALSE)");
    }

    [Test]
    public void Preferred_WithAnArgumentThatIsNotExactlyOne_IsNotACondition()
    {
        Assert.That(Error("PREFERRED(x > 1, flag)").Code, Is.EqualTo(Codes.ConditionNotExactlyOne));
    }

    [Test]
    public void Preferred_EndToEnd_PassesOverJunk_AndWarnsAboutNothingItPassedOver()
    {
        var catalog = TestCatalog.Standard()
            .With("x", new Unusable<decimal>(null))
            .With("y", new Usable<decimal>(5m))
            .With<decimal>("a", new Usable<decimal>(9m));

        var outcome = Evaluate("PREFERRED(x, y, a) == 5", catalog);

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Reported, Is.Empty);
        Assert.That(catalog.Resolutions("a"), Is.Zero);
    }
}
