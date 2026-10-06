using Goro.Messages;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;
using static Goro.Tests.Predicates.Support.CompileAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>Types: operands must agree, except where a number literal stands for a bytecount or a duration.</summary>
public class TypeTests
{
    [TestCase("artist == 1")]
    [TestCase("year < \"x\"")]
    [TestCase("file::size == file::duration")]
    [TestCase("x == TRUE")]
    [TestCase("year == file::size")]
    public void Mismatch_IsAnError(string text)
    {
        Assert.That(Error(text).Code, Is.EqualTo(Codes.TypeMismatch));
    }

    [TestCase("year == \"2000\"", "year == 2000")]
    [TestCase("file::duration > \"01:00\"", "file::duration > 01:00")]
    [TestCase("file::size > \"10kb\"", "file::size > 10kb")]
    [TestCase("file::size > \"10\"", "file::size > 10")]
    [TestCase("(x > 1) == \"TRUE\"", "(x > 1) == TRUE")]
    [TestCase("id3v2::raw::TRCK > 9", "id3v2::raw::TRCK AS NUMBER > 9")]
    [TestCase("year > artist", "year > artist AS NUMBER")]
    public void Mismatch_OffersAConversion(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.TypeMismatch));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void Mismatch_OfANonLiteralNumberWithABytecount_SaysOnlyALiteralStandsIn()
    {
        var error = Error("file::size > COUNT(genre)");

        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.TypeMismatchNumberVariable(
            new Code("file::size"), GoroType.ByteCount, new Code("COUNT(genre)"), GoroType.Number, new Code(">"))));
    }

    [TestCase("file::duration > (90)")]
    [TestCase("file::duration > 90")]
    [TestCase("file::duration > ((90))")]
    [TestCase("90 < file::duration")]
    [TestCase("ALL(90) < file::duration")]
    public void NumberLiteral_StandsForADuration_InSeconds(string text)
    {
        var comparison = (ComparisonTest<Duration>)Compiles(text).Root;

        var literal = comparison.Left.Expression as Literal<Duration> ?? (Literal<Duration>)comparison.Right.Expression;
        Assert.That(literal.Value, Is.EqualTo(new Duration(90)));
    }

    [Test]
    public void NumberLiteral_StandsForABytecount_InBytes()
    {
        var comparison = (ComparisonTest<ByteCount>)Compiles("file::size > 1000").Root;

        Assert.That(((Literal<ByteCount>)comparison.Right.Expression).Value, Is.EqualTo(new ByteCount(1000)));
    }

    [TestCase("file::duration > 1.5", Codes.FractionalDurationLiteral, "1.5")]
    [TestCase("file::duration > (1.5)", Codes.FractionalDurationLiteral, "1.5")]
    [TestCase("file::size > -1", Codes.NegativeUnitLiteral, "-1")]
    [TestCase("file::duration < -5", Codes.NegativeUnitLiteral, "-5")]
    public void NumberLiteral_ThatCannotStandIn_IsAnError(string text, string code, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(code));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
    }

    [Test]
    public void FractionalBytecount_FromANumberLiteral_IsExact()
    {
        var comparison = (ComparisonTest<ByteCount>)Compiles("file::size > 1.5").Root;

        Assert.That(((Literal<ByteCount>)comparison.Right.Expression).Value, Is.EqualTo(new ByteCount(1.5m)));
    }

    [TestCase("x > 9", true)]
    [TestCase("x > 9kb", false)]
    [TestCase("artist == 9", false)]
    public void NumberLiteral_StandsInOnlyForABytecountOrADuration(string text, bool valid)
    {
        if (valid)
        {
            Compiles(text);
        }
        else
        {
            Assert.That(Error(text).Code, Is.EqualTo(Codes.TypeMismatch));
        }
    }

    // A boolean as an operand of <, BETWEEN or =~, or as the operand of AS.
    [TestCase("(x < 1) < (y < 1)", Codes.BooleanNotOrdered)]
    [TestCase("(x < 1) >= TRUE", Codes.BooleanNotOrdered)]
    [TestCase("(x < 1) BETWEEN 1..2", Codes.BooleanNotOrdered)]
    [TestCase("x BETWEEN TRUE..FALSE", Codes.BooleanNotOrdered)]
    [TestCase("(x < 1) =~ r\"a\"", Codes.MatchSubjectNotString)]
    [TestCase("(x < 1) AS NUMBER > 1", Codes.BooleanNotConvertible)]
    [TestCase("(x < 1) AS STRING == \"a\"", Codes.BooleanNotConvertible)]
    public void Boolean_IsUnorderedAndNotConvertible(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    [TestCase("(a == b) == (c == d)")]
    [TestCase("(year < 2000) != (genre == \"rock\")")]
    [TestCase("(x < 1) == TRUE")]
    [TestCase("COUNT(x < 1) == 1")]
    [TestCase("FALLBACK(x < 1, TRUE)")]
    [TestCase("(x < 1) IS UNUSABLE")]
    public void Boolean_MayBeComparedForEquality_CountedAndSubstitutedFor(string text)
    {
        Compiles(text);
    }

    [Test]
    public void ChainedComparison_IsStillASyntaxError()
    {
        Assert.That(Error("a == b == c").Code, Is.EqualTo(Goro.Predicates.Syntax.SyntaxDiagnosticCodes.ChainedComparison));
    }
}
