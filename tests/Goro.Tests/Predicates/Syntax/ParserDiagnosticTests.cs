using Goro.Messages;
using Goro.Predicates.Diagnostics;
using static Goro.Tests.Predicates.Syntax.SyntaxAssert;
using Codes = Goro.Predicates.Syntax.SyntaxDiagnosticCodes;

namespace Goro.Tests.Predicates.Syntax;

/// <summary>
/// Syntax errors, and the rewrites offered for them. A rewrite is checked by applying it to the
/// predicate, which is what a user would do with it.
/// </summary>
public class ParserDiagnosticTests
{
    // ---------------------------------------------------------------------------------------------
    // The pattern of =~: docs/testing.md, Predicate reading.

    [Test]
    public void Pattern_Quoted_OffersTheRawForm()
    {
        const string text = @"artist =~ ""^a""";
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.QuotedPattern));
        Assert.That(error.Span.Of(text), Is.EqualTo(@"""^a"""));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { @"artist =~ r""^a""" }));
    }

    [TestCase(@"x =~ ""\\d{4}""", @"x =~ r""\d{4}""")]
    [TestCase(@"x =~ ""say \""hi\""""", @"x =~ r""say """"hi""""""")]
    public void Pattern_Quoted_RawFormDenotesTheSameText(string text, string rewrite)
    {
        Assert.That(Rewrites(text, Error(text)), Is.EqualTo(new[] { rewrite }));
        Parses(rewrite);
    }

    [TestCase(@"title =~ artist")]
    [TestCase(@"title =~ 5")]
    [TestCase(@"title =~ x AS NUMBER")]
    [TestCase(@"title =~ NULL")]
    public void Pattern_NotAString_IsRejected(string text)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.PatternNotRawString));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [Test]
    public void Pattern_Parenthesized_OffersItWithoutParentheses()
    {
        const string text = @"artist =~ (r""^a"")";
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.ParenthesizedPattern));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { @"artist =~ r""^a""" }));
    }

    [Test]
    public void Pattern_Literally_OffersItOnTheSubject()
    {
        const string text = @"artist =~ LITERALLY(r""^a"")";
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.ModifiedPattern));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { @"LITERALLY(artist) =~ r""^a""" }));
    }

    [TestCase(@"x == 1 AND artist =~ literally(""^a"")", @"x == 1 AND literally(artist) =~ r""^a""")]
    [TestCase(@"LITERALLY(artist) =~ LITERALLY(r""a"")", @"LITERALLY(artist) =~ r""a""")]
    [TestCase(@"artist =~ (LITERALLY((r""a"")))", @"LITERALLY(artist) =~ r""a""")]
    public void Pattern_Literally_RewriteIsComposedFromWhatWasWritten(string text, string rewrite)
    {
        Assert.That(Rewrites(text, Error(text)), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase(@"artist =~ ALL(r""^a"")")]
    [TestCase(@"artist =~ ANY(r""^a"")")]
    [TestCase(@"artist =~ LITERALLY(ALL(r""^a""))")]
    public void Pattern_Quantified_IsRejectedWithoutARewrite(string text)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.ModifiedPattern));
        Assert.That(error.Suggestions, Is.Empty);
    }

    // ---------------------------------------------------------------------------------------------
    // Borrowed operator spellings

    [Test]
    public void TildeEqual_OffersMatchAndInequality()
    {
        const string text = @"artist ~= r""^a""";
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.BorrowedOperator));
        Assert.That(error.Span.Of(text), Is.EqualTo("~="));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { @"artist =~ r""^a""", @"artist != r""^a""" }));
    }

    [Test]
    public void TildeEqual_WithQuotedString_OffersARawPattern()
    {
        const string text = @"artist ~= ""a""";
        Assert.That(Rewrites(text, Error(text)), Is.EqualTo(new[] { @"artist =~ r""a""", @"artist != ""a""" }));
    }

    [TestCase("artist ~= title")]
    [TestCase("artist ~= ")]
    [TestCase("artist ~= (")]
    public void TildeEqual_WithoutAStringAfterIt_OffersOnlyInequality(string text)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.BorrowedOperator));
        Assert.That(error.Suggestions.Select(s => s.Replacement), Is.EqualTo(new[] { "!=" }));
    }

    [TestCase(@"artist !~ r""^a""", @"NOT artist =~ r""^a""")]
    [TestCase(@"artist!~""^a""", @"NOT artist=~r""^a""")]
    [TestCase(@"x == 1 OR LITERALLY(artist) !~ r""a""", @"x == 1 OR NOT LITERALLY(artist) =~ r""a""")]
    public void BangTilde_OffersNotWithAMatch(string text, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.BorrowedOperator));
        Assert.That(error.Span.Of(text), Is.EqualTo("!~"));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("artist = \"x\"", "artist == \"x\"")]
    [TestCase("year <> 2000", "year != 2000")]
    [TestCase("a == 1 && b == 2", "a == 1 AND b == 2")]
    [TestCase("a == 1&&b == 2", "a == 1 AND b == 2")]
    [TestCase("a == 1 || b == 2", "a == 1 OR b == 2")]
    [TestCase("(a)||(b)", "(a) OR (b)")]
    [TestCase("!a", "NOT a")]
    [TestCase("! a", "NOT a")]
    [TestCase("x == 1 AND !(y == 2)", "x == 1 AND NOT (y == 2)")]
    [TestCase("(!a)", "(NOT a)")]
    [TestCase("x == 1 AND x = 2", "x == 1 AND x == 2")]
    public void BorrowedSpelling_OffersTheOneMeant(string text, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.BorrowedOperator));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
        Parses(rewrite);
    }

    // ---------------------------------------------------------------------------------------------
    // Chained comparisons

    [TestCase("a == b == c", "a == b AND b == c")]
    [TestCase("1 < x < 10", "1 < x AND x < 10")]
    [TestCase("1<x<=10", "1<x AND x<=10")]
    [TestCase("a == f(b, c) IS ABSENT", "a == f(b, c) AND f(b, c) IS ABSENT")]
    public void ChainedComparison_OffersAnd(string text, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.ChainedComparison));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
        Parses(rewrite);
    }

    [TestCase("x IS ABSENT == TRUE", "==")]
    [TestCase("x BETWEEN 1..2 BETWEEN 3..4", "BETWEEN")]
    [TestCase("x =~ r\"a\" =~ r\"b\"", "=~")]
    [TestCase("a == b = c", "=")]
    [TestCase("a < b ~= c", "~=")]
    public void ChainedComparison_IsReportedAsSuch(string text, string second)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.ChainedComparison));
        Assert.That(error.Span.Of(text), Is.EqualTo(second));
    }

    // ---------------------------------------------------------------------------------------------
    // Reserved words

    [TestCase("and == 1", "and")]
    [TestCase("x == between", "between")]
    [TestCase("usable", "usable")]
    [TestCase("x == ABSENT", "ABSENT")]
    [TestCase("all == 1", "all")]
    [TestCase("LITERALLY genre == 1", "LITERALLY")]
    [TestCase("as == 1", "as")]
    public void ReservedWord_AsABareIdentifier_IsAnError(string text, string word)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.ReservedWord));
        Assert.That(error.Span.Of(text), Is.EqualTo(word));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [TestCase("and::x", "and")]
    [TestCase("x == true::y", "true")]
    [TestCase("NULL::x == 1", "NULL")]
    [TestCase("ALL::x == 1", "ALL")]
    [TestCase("x == IS::y", "IS")]
    [TestCase("ape::and == 1", "and")]
    [TestCase("vorbis::NULL == 1", "NULL")]
    public void ReservedWord_InAnIdentifier_IsAnError(string text, string word)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.ReservedWordInIdentifier));
        Assert.That(error.Span.Of(text), Is.EqualTo(word));
    }

    // ---------------------------------------------------------------------------------------------
    // Identifiers and function calls

    [TestCase("::artist == 1", "::artist", "artist == 1")]
    [TestCase("::file::size > 1", "::file::size", "file::size > 1")]
    [TestCase("::count(x) > 1", "::count", "count(x) > 1")]
    [TestCase("NOT ::x", "::x", "NOT x")]
    public void LeadingDoubleColon_OffersTheNameWithoutIt(string text, string marked, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.RootedIdentifier));
        Assert.That(error.Span.Of(text), Is.EqualTo(marked));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("foo::bar::baz", "foo::bar::baz")]
    [TestCase("id3v1::raw::genre == 1", "id3v1::raw::genre")]
    [TestCase("a::b::fallback(x, 1)", "a::b::fallback")]
    public void ASecondDoubleColon_IsAnError_ASourceBeingOneLevelDeep(string text, string marked)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.NestedIdentifier));
        Assert.That(error.Span.Of(text), Is.EqualTo(marked));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [TestCase("ape::\"Album Artist\" == \"x\"", "ape::field(\"Album Artist\") == \"x\"")]
    [TestCase("ape::r\"Album Artist\" == \"x\"", "ape::field(r\"Album Artist\") == \"x\"")]
    [TestCase("vorbis::\"mood\"::x", "vorbis::field(\"mood\")::x")]
    public void AQuotedName_OffersField(string text, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.QuotedName));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("ape :: artist == 1", "ape::artist == 1")]
    [TestCase("ape:: artist == 1", "ape::artist == 1")]
    [TestCase("ape ::artist == 1", "ape::artist == 1")]
    [TestCase("vorbis :: field(\"x\") == 1", "vorbis::field(\"x\") == 1")]
    public void WhitespaceAroundDoubleColon_IsAnError_AndOffersTheIdentifierWithout(string text, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.WhitespaceInIdentifier));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("ape:: == 1", Codes.UnexpectedToken)]
    [TestCase("ape::", Codes.UnexpectedEnd)]
    [TestCase(":: == 1", Codes.RootedIdentifier)]
    [TestCase("ape::5", Codes.UnexpectedToken)]
    public void Identifier_WithoutANameAfterDoubleColon_IsAnError(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    [TestCase("x AS > 1", Codes.UnexpectedToken)]
    [TestCase("x AS", Codes.UnexpectedEnd)]
    [TestCase("x AS 5 > 1", Codes.UnexpectedToken)]
    [TestCase("x AS \"NUMBER\" > 1", Codes.UnexpectedToken)]
    [TestCase("x AS ::NUMBER > 1", Codes.UnexpectedToken)]
    public void As_WithoutATargetName_IsAnError(string text, string code)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(code));
        var expected = error.Message switch
        {
            Goro.Messages.ErrorMessage.UnexpectedEnd end => end.Expected,
            Goro.Messages.ErrorMessage.UnexpectedToken token => token.Expected,
            var other => throw new AssertionException($"Unexpected message {other}"),
        };
        Assert.That(expected, Is.EqualTo(Goro.Messages.Expectation.Target));
    }

    // ---------------------------------------------------------------------------------------------
    // Ranges

    [Test]
    public void Range_ParenthesizedEndpoint_OffersItBare()
    {
        const string text = "x BETWEEN (1)..2";
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.RangeEndpointNotLiteral));
        Assert.That(error.Span.Of(text), Is.EqualTo("(1)"));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { "x BETWEEN 1..2" }));
    }

    [TestCase("x BETWEEN 1..(2)")]
    [TestCase("x BETWEEN y..2")]
    [TestCase("x BETWEEN NULL..2")]
    [TestCase("x BETWEEN 1..ALL(2)")]
    [TestCase("x BETWEEN (y)..3")]
    public void Range_EndpointThatIsNotALiteral_IsAnError(string text)
    {
        Assert.That(Error(text).Code, Is.EqualTo(Codes.RangeEndpointNotLiteral));
    }

    [TestCase("(1)..2", Codes.UnexpectedToken)]
    [TestCase("x == 1..2", Codes.UnexpectedToken)]
    [TestCase("x BETWEEN 1", Codes.UnexpectedEnd)]
    [TestCase("x BETWEEN 1 2", Codes.UnexpectedToken)]
    [TestCase("x BETWEEN", Codes.UnexpectedEnd)]
    public void Range_OutOfPlaceOrIncomplete_IsAnError(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    // ---------------------------------------------------------------------------------------------
    // State tests

    [TestCase("x IS NOT ABSENT", "NOT (x IS ABSENT)")]
    [TestCase("ALL(x) is not usable", "NOT (ALL(x) is usable)")]
    [TestCase("x IS NOT NULL", "NOT (x IS ABSENT)")]
    public void IsNot_OffersNotAroundTheTest(string text, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.IsNot));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
        Parses(rewrite);
    }

    [Test]
    public void IsNull_OffersAbsent()
    {
        const string text = "year IS NULL";
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.IsNull));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { "year IS ABSENT" }));
    }

    [TestCase("x IS", Codes.UnexpectedEnd)]
    [TestCase("x IS TRUE", Codes.UnexpectedToken)]
    [TestCase("x IS NOT", Codes.IsNot)]
    public void StateTest_WithoutAStateName_IsAnError(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    // ---------------------------------------------------------------------------------------------
    // Literals the lexer let through, which then run into the text after them

    [TestCase("x == 5BETWEEN 1..10", "5BETWEEN")]
    [TestCase("x == 5x", "5x")]
    [TestCase("x == 5bytes", "5bytes")]
    [TestCase("x == 5ms", "5ms")]
    [TestCase("x == 1h10", "1h10")]
    [TestCase("x == 10m1h", "10m1h")]
    [TestCase("x == 1:000", "1:000")]
    [TestCase("x == 5NOT y", "5NOT")]
    public void LiteralRunningIntoText_IsMalformed(string text, string run)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.MalformedLiteral));
        Assert.That(error.Span.Of(text), Is.EqualTo(run));
    }

    [Test]
    public void LiteralRunningIntoText_SaysHowItWasRead()
    {
        Assert.That(Error("5BETWEEN 1..10").Message, Is.EqualTo(
            new ErrorMessage.RunOnLiteral(new Code("5BETWEEN"), new Code("5B"), NumericLiteral.ByteCount, new Code("ETWEEN"))));
    }

    [Test]
    public void FractionalDuration_SaysFieldsAreWhole()
    {
        var error = Error("file::duration > 1.5h");
        Assert.That(error.Code, Is.EqualTo(Codes.MalformedLiteral));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.FractionalDurationFields(new Code("1.5h"))));
    }

    [TestCase("x > -5mb", "-5mb")]
    [TestCase("x > -1h", "-1h")]
    [TestCase("x > +2KiB", "+2KiB")]
    public void SignedBytecountOrDuration_IsAnError(string text, string run)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.SignedUnitLiteral));
        Assert.That(error.Span.Of(text), Is.EqualTo(run));
    }

    [TestCase("file::size > 10 kb", "file::size > 10kb")]
    [TestCase("file::duration > 1h 10m", "file::duration > 1h10m")]
    [TestCase("file::duration > 1h 10m", "file::duration > 1h10m")]
    public void WhitespaceInsideALiteral_OffersItWithout(string text, string rewrite)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.WhitespaceInLiteral));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("x == 5 5")]
    [TestCase("x == 5 kg")]
    public void TwoTokensThatAreNotOneLiteral_AreMerelyUnexpected(string text)
    {
        var error = Error(text);
        Assert.That(error.Code, Is.EqualTo(Codes.UnexpectedToken));
        Assert.That(error.Suggestions, Is.Empty);
    }

    // ---------------------------------------------------------------------------------------------
    // Everything else

    [TestCase("")]
    [TestCase("   ")]
    public void EmptyPredicate_IsAnError(string text)
    {
        Assert.That(Error(text).Code, Is.EqualTo(Codes.EmptyPredicate));
    }

    [TestCase("x ==", Codes.UnexpectedEnd)]
    [TestCase("(x == 1", Codes.UnexpectedEnd)]
    [TestCase("f(x", Codes.UnexpectedEnd)]
    [TestCase("f(x,)", Codes.UnexpectedToken)]
    [TestCase("x == 1)", Codes.UnexpectedToken)]
    [TestCase("artist \"metallica\"", Codes.UnexpectedToken)]
    [TestCase("x == NOT y", Codes.UnexpectedToken)]
    [TestCase("ALL(genre == 1)", Codes.UnexpectedToken)]
    [TestCase("ALL()", Codes.UnexpectedToken)]
    [TestCase("x == 1 AND", Codes.UnexpectedEnd)]
    [TestCase("TRUE()", Codes.UnexpectedToken)]
    [TestCase(", x", Codes.UnexpectedToken)]
    public void Unexpected_SaysWhatWasExpected(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    [Test]
    public void Unexpected_PointsAtTheToken()
    {
        const string text = "x == 1 y";
        var error = Error(text);
        Assert.That(error.Span.Of(text), Is.EqualTo("y"));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.UnexpectedToken(Expectation.OperatorOrEnd, new Code("y"))));
    }

    [Test]
    public void LexicalError_IsReportedBeforeAnySyntaxError()
    {
        Assert.That(Error(@"== ""\q""").Code, Is.EqualTo(Codes.UnknownEscape));
    }

    [Test]
    public void OnlyTheFirstSyntaxError_IsReported()
    {
        const string text = "artist = \"metallica\" && year = 2000";
        var error = Error(text);
        Assert.That(error.Span, Is.EqualTo(new TextSpan(7, 1)));
    }
}
