using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using static Goro.Tests.Predicates.Syntax.SyntaxAssert;
using Codes = Goro.Predicates.Syntax.SyntaxDiagnosticCodes;

namespace Goro.Tests.Predicates.Syntax;

public class LexerTests
{
    // ---------------------------------------------------------------------------------------------
    // Quoted strings. docs/testing.md, Predicate reading: every escape produces exactly its
    // character, and nothing else is an escape.

    [TestCase(@"""\""""", "\"")]
    [TestCase(@"""\\""", "\\")]
    [TestCase(@"""\n""", "\n")]
    [TestCase(@"""\r""", "\r")]
    [TestCase(@"""\t""", "\t")]
    [TestCase(@"""\x41""", "A")]
    [TestCase(@"""é""", "é")]
    [TestCase(@"""\u{1F3B5}""", "\U0001F3B5")]
    public void QuotedString_EachEscape_ProducesExactlyItsCharacter(string literal, string expected)
    {
        Assert.That(StringValue(literal), Is.EqualTo(expected));
    }

    [TestCase(@"""\x41B""")]
    [TestCase(@"""AB""")]
    public void QuotedString_FixedWidthEscapes_TakeExactlyTheirDigits(string literal)
    {
        Assert.That(StringValue(literal), Is.EqualTo("AB"));
    }

    [Test]
    public void QuotedString_HexDigitsInEitherCase_AreTheSame()
    {
        Assert.That(StringValue(@"""\x4a\x4AJ\u{4A}"""), Is.EqualTo("JJJJ"));
    }

    [Test]
    public void QuotedString_TextAroundEscapes_IsKeptExactly()
    {
        Assert.That(StringValue(@"""  a\tb  """), Is.EqualTo("  a\tb  "));
    }

    [TestCase(@"""\N""", @"\N")]
    [TestCase(@"""\U0041""", @"\U")]
    [TestCase(@"""\d{4}""", @"\d")]
    [TestCase(@"""\b""", @"\b")]
    [TestCase(@"""\'""", @"\'")]
    [TestCase(@"""\X41""", @"\X")]
    [TestCase(@"""a\ b""", @"\ ")]
    public void QuotedString_BackslashBeforeAnythingElse_IsAnError(string literal, string escape)
    {
        var error = LexError(literal);
        Assert.That(error.Code, Is.EqualTo(Codes.UnknownEscape));
        Assert.That(error.Span.Of(literal), Is.EqualTo(escape));
    }

    [TestCase(@"""\x4""")]
    [TestCase(@"""\xG1""")]
    [TestCase(@"""\u004""")]
    [TestCase(@"""\u{}""")]
    [TestCase(@"""\u{1234567}""")]
    [TestCase(@"""\u{41""")]
    public void QuotedString_EscapeWithWrongDigits_IsMalformed(string literal)
    {
        Assert.That(LexError(literal).Code, Is.EqualTo(Codes.MalformedEscape));
    }

    [Test]
    public void QuotedString_BracedEscape_IsOneCharacterOutsideTheBmp()
    {
        var value = StringValue(@"""\u{1F3B5}""");
        Assert.That(value.EnumerateRunes().Single().Value, Is.EqualTo(0x1F3B5));
    }

    [Test]
    public void QuotedString_BracedEscape_TakesOneToSixDigits()
    {
        Assert.That(StringValue(@"""\u{9}\u{00041}\u{10FFFF}"""), Is.EqualTo("\tA\U0010FFFF"));
    }

    [TestCase(@"""\u{D800}""", Codes.SurrogateCodePoint)]
    [TestCase(@"""\u{dfff}""", Codes.SurrogateCodePoint)]
    [TestCase(@"""\u{110000}""", Codes.CodePointOutOfRange)]
    [TestCase(@"""\u{FFFFFF}""", Codes.CodePointOutOfRange)]
    public void QuotedString_BracedEscapeThatIsNotACharacter_IsAnError(string literal, string code)
    {
        Assert.That(LexError(literal).Code, Is.EqualTo(code));
    }

    [Test]
    public void QuotedString_SurrogatePairOfEscapes_IsTheCharacterTheyEncode()
    {
        Assert.That(StringValue(@"""🎵"""), Is.EqualTo(StringValue(@"""\u{1F3B5}""")));
    }

    [TestCase(@"""\uD83C""", @"\uD83C")]
    [TestCase(@"""\uD83Cx""", @"\uD83C")]
    [TestCase(@"""\uD83C\n""", @"\uD83C")]
    [TestCase(@"""\uD83C\uD83C""", @"\uD83C")]
    [TestCase(@"""\uDFB5""", @"\uDFB5")]
    [TestCase(@"""x\uDFB5\uD83C""", @"\uDFB5")]
    public void QuotedString_SurrogateNotPartOfAPair_IsAnError(string literal, string escape)
    {
        var error = LexError(literal);
        Assert.That(error.Code, Is.EqualTo(Codes.LoneSurrogate));
        Assert.That(error.Span.Of(literal), Is.EqualTo(escape));
    }

    [TestCase(@"""abc")]
    [TestCase(@"""abc\""")]
    [TestCase(@"""abc\")]
    [TestCase(@"r""abc")]
    [TestCase(@"r""abc""""")]
    public void String_WithoutClosingQuote_IsUnterminated(string literal)
    {
        Assert.That(LexError(literal).Code, Is.EqualTo(Codes.UnterminatedString));
    }

    // ---------------------------------------------------------------------------------------------
    // Raw strings: what was written, byte for byte.

    [TestCase(@"r""\d{4}""", @"\d{4}")]
    [TestCase(@"R""\n""", @"\n")]
    [TestCase(@"r""a""""b""", "a\"b")]
    [TestCase(@"r""""""""", "\"")]
    [TestCase(@"r""""", "")]
    [TestCase(@"r""say """"hi""""""", "say \"hi\"")]
    public void RawString_IsWhatWasWritten(string literal, string expected)
    {
        var token = (StringToken)SingleToken(literal);
        Assert.That(token.Value, Is.EqualTo(expected));
        Assert.That(token.IsRaw, Is.True);
    }

    [Test]
    public void RawString_PrefixSeparatedFromQuote_IsANameAndAString()
    {
        var tokens = Lexes(@"r ""x""");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[] { TokenKind.Name, TokenKind.String, TokenKind.EndOfText }));
        Assert.That(((StringToken)tokens[1]).IsRaw, Is.False);
    }

    [TestCase(@"r == 1")]
    [TestCase(@"r==1")]
    [TestCase(@"r::x")]
    [TestCase(@"r")]
    public void NameR_NotFollowedByAQuote_IsAName(string text)
    {
        var first = Lexes(text)[0];
        Assert.That(first, Is.TypeOf<NameToken>());
        Assert.That(((NameToken)first).Text, Is.EqualTo("r"));
    }

    [TestCase(@"""a\\b""", @"r""a\b""")]
    [TestCase(@"""say \""hi\""""", @"r""say """"hi""""""")]
    [TestCase(@"""\u{1F3B5}""", "r\"\U0001F3B5\"")]
    public void QuotedAndRawString_DenotingTheSameCharacters_HaveTheSameValue(string quoted, string raw)
    {
        Assert.That(StringValue(quoted), Is.EqualTo(StringValue(raw)));
    }

    // ---------------------------------------------------------------------------------------------
    // Numbers

    [TestCase("2000", "2000")]
    [TestCase("-1", "-1")]
    [TestCase("+5", "5")]
    [TestCase("-.55", "-0.55")]
    [TestCase(".5", "0.5")]
    [TestCase("1.50", "1.50")]
    [TestCase("79228162514264337593543950335", "79228162514264337593543950335")]
    public void Number_IsReadWithItsSign(string literal, string expected)
    {
        var token = (NumberToken)SingleToken(literal);
        Assert.That(token.Value, Is.EqualTo(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TestCase("1.")]
    [TestCase("1. ")]
    [TestCase("x == 1.")]
    [TestCase("1.x")]
    public void Number_WithTrailingPeriod_IsAnError(string text)
    {
        var error = LexError(text);
        Assert.That(error.Code, Is.EqualTo(Codes.TrailingPeriod));
        Assert.That(error.Span.Of(text), Is.EqualTo("1."));
    }

    [TestCase("79228162514264337593543950336")]
    [TestCase("0.12345678901234567890123456789")]
    [TestCase("-79228162514264337593543950336")]
    public void Number_ThatCannotBeHeldExactly_IsAnError(string literal)
    {
        Assert.That(LexError(literal).Code, Is.EqualTo(Codes.UnrepresentableLiteral));
    }

    [TestCase("1..100", TokenKind.Number, TokenKind.DotDot, TokenKind.Number)]
    [TestCase("1.5..2", TokenKind.Number, TokenKind.DotDot, TokenKind.Number)]
    [TestCase("-5..-.5", TokenKind.Number, TokenKind.DotDot, TokenKind.Number)]
    [TestCase("1kb..2mib", TokenKind.ByteCount, TokenKind.DotDot, TokenKind.ByteCount)]
    [TestCase("1:00..2h", TokenKind.Duration, TokenKind.DotDot, TokenKind.Duration)]
    [TestCase(@"""a"".."" b""", TokenKind.String, TokenKind.DotDot, TokenKind.String)]
    public void Range_SurvivesTheLexer(string text, TokenKind min, TokenKind dots, TokenKind max)
    {
        Assert.That(Lexes(text).Select(t => t.Kind), Is.EqualTo(new[] { min, dots, max, TokenKind.EndOfText }));
    }

    [Test]
    public void Range_EndpointsKeepTheirValues()
    {
        var tokens = Lexes("1.5..2");
        Assert.That(((NumberToken)tokens[0]).Value, Is.EqualTo(1.5m));
        Assert.That(((NumberToken)tokens[2]).Value, Is.EqualTo(2m));
    }

    // ---------------------------------------------------------------------------------------------
    // Bytecounts

    [TestCase("100b", "100")]
    [TestCase("1kb", "1000")]
    [TestCase("1mb", "1000000")]
    [TestCase("1gb", "1000000000")]
    [TestCase("1tb", "1000000000000")]
    [TestCase("1kib", "1024")]
    [TestCase("1mib", "1048576")]
    [TestCase("1gib", "1073741824")]
    [TestCase("1tib", "1099511627776")]
    [TestCase("10KiB", "10240")]
    [TestCase("1.4kib", "1433.6")]
    [TestCase("1.4mb", "1400000")]
    [TestCase(".5Kb", "500")]
    [TestCase("0b", "0")]
    public void ByteCount_IsExactlyItsBytes(string literal, string bytes)
    {
        var token = (ByteCountToken)SingleToken(literal);
        Assert.That(token.Value.Bytes, Is.EqualTo(decimal.Parse(bytes, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TestCase("KB")]
    [TestCase("kb")]
    [TestCase("Kb")]
    [TestCase("kB")]
    public void ByteCount_UnitIsMatchedWithoutRegardToCase(string unit)
    {
        Assert.That(((ByteCountToken)SingleToken("5" + unit)).Value.Bytes, Is.EqualTo(5000m));
    }

    [TestCase("79228162514264337593543950335kb")]
    [TestCase("0.1234567890123456789012345678kib")]
    public void ByteCount_ThatCannotBeHeldExactly_IsAnError(string literal)
    {
        Assert.That(LexError(literal).Code, Is.EqualTo(Codes.UnrepresentableLiteral));
    }

    [Test]
    public void ByteCount_SmallFractionThatScalesExactly_IsAccepted()
    {
        // 0.0000000000000000000000000001 is a decimal's smallest step; times 1000 it is still exact.
        Assert.That(((ByteCountToken)SingleToken("0.0000000000000000000000000001kb")).Value.Bytes,
            Is.EqualTo(0.0000000000000000000000001m));
    }

    // ---------------------------------------------------------------------------------------------
    // Durations

    [TestCase("1h10m", 4200)]
    [TestCase("1h1s", 3601)]
    [TestCase("90m", 5400)]
    [TestCase("1h100m", 9600)]
    [TestCase("0s", 0)]
    [TestCase("2H3M4S", 7384)]
    [TestCase("1:59", 119)]
    [TestCase("01:59", 119)]
    [TestCase("0:00", 0)]
    [TestCase("3:30", 210)]
    [TestCase("90:00", 5400)]
    [TestCase("250:00:00", 900000)]
    [TestCase("1:02:03", 3723)]
    public void Duration_IsItsSeconds(string literal, int seconds)
    {
        Assert.That(((DurationToken)SingleToken(literal)).Value.Seconds, Is.EqualTo((decimal)seconds));
    }

    [TestCase("1:60", "60")]
    [TestCase("1:99:00", "99")]
    [TestCase("1:00:60", "60")]
    public void Duration_ClockFieldAfterTheFirstAbove59_IsAnError(string literal, string field)
    {
        var error = LexError(literal);
        Assert.That(error.Code, Is.EqualTo(Codes.ClockFieldOutOfRange));
        Assert.That(error.Span.Of(literal), Is.EqualTo(field));
    }

    [TestCase("1:5")]
    [TestCase("1:00:5")]
    [TestCase("1:00:00:00")]
    public void Duration_ClockWithMalformedFields_IsAnError(string literal)
    {
        var error = LexError(literal);
        Assert.That(error.Code, Is.EqualTo(Codes.MalformedLiteral));
        Assert.That(error.Span.Of(literal), Is.EqualTo(literal));
    }

    [Test]
    public void Duration_ClockWithSign_IsAnError()
    {
        Assert.That(LexError("-1:30").Code, Is.EqualTo(Codes.SignedUnitLiteral));
    }

    [Test]
    public void Duration_AtTheLargestDecimal_IsAccepted()
    {
        Assert.That(((DurationToken)SingleToken("79228162514264337593543950335s")).Value.Seconds, Is.EqualTo(decimal.MaxValue));
    }

    [TestCase("79228162514264337593543950336s")]
    [TestCase("79228162514264337593543950335m")]
    [TestCase("22007822920628982664873319538:00:00")]
    public void Duration_ThatCannotBeHeldExactly_IsAnError(string literal)
    {
        Assert.That(LexError(literal).Code, Is.EqualTo(Codes.UnrepresentableLiteral));
    }

    // ---------------------------------------------------------------------------------------------
    // The longest token wins, asserted where it decides something.

    [TestCase("5mb", TokenKind.ByteCount)]
    [TestCase("5mib", TokenKind.ByteCount)]
    [TestCase("5m", TokenKind.Duration)]
    [TestCase("1h10m", TokenKind.Duration)]
    [TestCase("5b", TokenKind.ByteCount)]
    [TestCase("NOTx", TokenKind.Name)]
    [TestCase("ANDROID", TokenKind.Name)]
    [TestCase("r\"x\"", TokenKind.String)]
    [TestCase("!=", TokenKind.BangEqual)]
    [TestCase("<=", TokenKind.LessEqual)]
    [TestCase("<>", TokenKind.LessGreater)]
    [TestCase("=~", TokenKind.EqualTilde)]
    [TestCase("::", TokenKind.DoubleColon)]
    public void LongestToken_Wins(string text, TokenKind kind)
    {
        Assert.That(SingleToken(text).Kind, Is.EqualTo(kind));
    }

    [TestCase("10 kb", TokenKind.Number, TokenKind.Name)]
    [TestCase("1h 10m", TokenKind.Duration, TokenKind.Duration)]
    [TestCase("5m b", TokenKind.Duration, TokenKind.Name)]
    [TestCase("5AND", TokenKind.Number, TokenKind.And)]
    [TestCase("-5BETWEEN", TokenKind.Number, TokenKind.Between)]
    [TestCase("5BETWEEN", TokenKind.ByteCount, TokenKind.Name)]
    [TestCase("1.5h", TokenKind.Number, TokenKind.Name)]
    [TestCase("5ms", TokenKind.Duration, TokenKind.Name)]
    [TestCase("-5mb", TokenKind.Number, TokenKind.Name)]
    [TestCase("1h10", TokenKind.Duration, TokenKind.Number)]
    [TestCase("1:000", TokenKind.Duration, TokenKind.Number)]
    public void Literal_EndsWhereTheLongestTokenEnds(string text, TokenKind first, TokenKind second)
    {
        Assert.That(Lexes(text).Select(t => t.Kind), Is.EqualTo(new[] { first, second, TokenKind.EndOfText }));
    }

    [Test]
    public void Literal_BytecountRunningIntoAName_IsTheBytecountAndTheRest()
    {
        var tokens = Lexes("5BETWEEN");
        Assert.That(((ByteCountToken)tokens[0]).Value.Bytes, Is.EqualTo(5m));
        Assert.That(((NameToken)tokens[1]).Text, Is.EqualTo("ETWEEN"));
    }

    [TestCase("=", TokenKind.Equal)]
    [TestCase("<>", TokenKind.LessGreater)]
    [TestCase("&&", TokenKind.AmpersandAmpersand)]
    [TestCase("||", TokenKind.BarBar)]
    [TestCase("!", TokenKind.Bang)]
    [TestCase("!~", TokenKind.BangTilde)]
    [TestCase("~=", TokenKind.TildeEqual)]
    public void BorrowedSpelling_IsAToken(string text, TokenKind kind)
    {
        Assert.That(SingleToken(text).Kind, Is.EqualTo(kind));
    }

    [TestCase("and", TokenKind.And)]
    [TestCase("Or", TokenKind.Or)]
    [TestCase("NOT", TokenKind.Not)]
    [TestCase("between", TokenKind.Between)]
    [TestCase("is", TokenKind.Is)]
    [TestCase("usable", TokenKind.Usable)]
    [TestCase("UnUsable", TokenKind.Unusable)]
    [TestCase("absent", TokenKind.Absent)]
    [TestCase("TRUE", TokenKind.True)]
    [TestCase("true", TokenKind.True)]
    [TestCase("False", TokenKind.False)]
    [TestCase("null", TokenKind.Null)]
    [TestCase("all", TokenKind.All)]
    [TestCase("Any", TokenKind.Any)]
    [TestCase("literally", TokenKind.Literally)]
    public void ReservedWord_IsAKeywordWhateverItsCase(string text, TokenKind kind)
    {
        Assert.That(SingleToken(text).Kind, Is.EqualTo(kind));
    }

    [TestCase("count")]
    [TestCase("FALLBACK")]
    [TestCase("x_1")]
    [TestCase("Id3v2")]
    public void Name_KeepsItsSpelling(string text)
    {
        Assert.That(((NameToken)SingleToken(text)).Text, Is.EqualTo(text));
    }

    // ---------------------------------------------------------------------------------------------
    // Whitespace

    [Test]
    public void Whitespace_IsExactlyUnicodeWhiteSpace()
    {
        // The White_Space property, from the Unicode Character Database's PropList.txt.
        int[] whiteSpace =
        [
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x20, 0x85, 0xA0, 0x1680,
            0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200A,
            0x2028, 0x2029, 0x202F, 0x205F, 0x3000,
        ];
        foreach (var c in whiteSpace)
        {
            Assert.That(Lexes($"a{(char)c}b").Select(t => t.Kind),
                Is.EqualTo(new[] { TokenKind.Name, TokenKind.Name, TokenKind.EndOfText }), $"U+{c:X4}");
        }

        foreach (var c in new[] { 0x180E, 0x200B, 0xFEFF })
        {
            Assert.That(LexError($"a{(char)c}b").Code, Is.EqualTo(Codes.UnexpectedCharacter), $"U+{c:X4}");
        }
    }

    [Test]
    public void Whitespace_InsideAString_IsKept()
    {
        Assert.That(StringValue("\"foo  \"" ), Is.EqualTo("foo  "));
    }

    // ---------------------------------------------------------------------------------------------
    // Characters that begin no token

    [TestCase("größe == 1", "größe")]
    [TestCase("ape::größe", "größe")]
    [TestCase("éa", "éa")]
    [TestCase("x1é_2 == 1", "x1é_2")]
    [TestCase("ﬁle", "ﬁle")]
    public void Name_WithANonAsciiCharacter_IsAnError(string text, string word)
    {
        var error = LexError(text);
        Assert.That(error.Code, Is.EqualTo(Codes.NonAsciiName));
        Assert.That(error.Span.Of(text), Is.EqualTo(word));
    }

    [TestCase("x @ 1", "@")]
    [TestCase("x : 1", ":")]
    [TestCase("x & y", "&")]
    [TestCase("x | y", "|")]
    [TestCase("~", "~")]
    [TestCase("_x", "_")]
    [TestCase("- 1", "-")]
    [TestCase("x . y", ".")]
    [TestCase("x 🎵", "🎵")]
    public void Character_ThatBeginsNoToken_IsAnError(string text, string character)
    {
        var error = LexError(text);
        Assert.That(error.Code, Is.EqualTo(Codes.UnexpectedCharacter));
        Assert.That(error.Span.Of(text), Is.EqualTo(character));
    }

    // ---------------------------------------------------------------------------------------------

    [Test]
    public void Tokens_CarryTheirSpans()
    {
        const string text = "  ape::\"album artist\"  >=  1.4kib ";
        var tokens = Lexes(text);
        Assert.That(tokens.Select(t => t.Span.Of(text)),
            Is.EqualTo(new[] { "ape", "::", "\"album artist\"", ">=", "1.4kib", "" }));
        Assert.That(tokens[^1].Span, Is.EqualTo(new TextSpan(text.Length, 0)));
    }

    [Test]
    public void EmptyText_IsJustTheEnd()
    {
        Assert.That(Lexes(" \t ").Select(t => t.Kind), Is.EqualTo(new[] { TokenKind.EndOfText }));
    }
}
