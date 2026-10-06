// Owned by the evaluator group (G5) of the predicate-runtime-architecture line.
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// The conversions behind <c>AS</c>, one per pair of types it converts between. Each either gives the
/// converted datum or fails, and a failure becomes an unusable occurrence whose source is the
/// conversion.
/// </summary>
/// <remarks>
/// Text converts through the literal syntax of its target, read by the lexer, so that a string a
/// conversion accepts is exactly a literal of that type; for a duration or a bytecount a plain number
/// is accepted as well, on the terms of the numeric literal exception. The other direction writes the
/// canonical literal of the datum's own type, in its base unit, which reads back to the same value.
/// </remarks>
public static class Conversions
{
    /// <summary>Fails for text that is not a number literal, or whose value a number cannot hold exactly.</summary>
    public static bool NumberFromString(string datum, out decimal number) => NumberText.TryParse(datum, out number);

    /// <summary>A duration literal in either form, or a number literal taken as seconds.</summary>
    public static bool DurationFromString(string datum, out Duration duration)
    {
        duration = default;
        return OnlyLiteral(datum) switch
        {
            DurationToken literal => Got(literal.Value, out duration),
            NumberToken number => DurationFromNumber(number.Value, out duration),
            _ => false,
        };
    }

    /// <summary>A bytecount literal, or a number literal taken as bytes.</summary>
    public static bool ByteCountFromString(string datum, out ByteCount bytes)
    {
        bytes = default;
        return OnlyLiteral(datum) switch
        {
            ByteCountToken literal => Got(literal.Value, out bytes),
            NumberToken number => ByteCountFromNumber(number.Value, out bytes),
            _ => false,
        };
    }

    public static bool NumberFromByteCount(ByteCount datum, out decimal number)
    {
        number = datum.Bytes;
        return true;
    }

    public static bool NumberFromDuration(Duration datum, out decimal number)
    {
        number = datum.Seconds;
        return true;
    }

    /// <summary>That many seconds, which must be whole and not negative.</summary>
    public static bool DurationFromNumber(decimal datum, out Duration duration)
    {
        duration = new Duration(datum);
        return datum >= 0 && datum == decimal.Truncate(datum);
    }

    /// <summary>That many bytes, which must not be negative.</summary>
    public static bool ByteCountFromNumber(decimal datum, out ByteCount bytes)
    {
        bytes = new ByteCount(datum);
        return datum >= 0;
    }

    public static bool StringFromNumber(decimal datum, out string text)
    {
        text = NumberText.Format(datum);
        return true;
    }

    public static bool StringFromByteCount(ByteCount datum, out string text)
    {
        text = NumberText.Format(datum.Bytes) + "b";
        return true;
    }

    public static bool StringFromDuration(Duration datum, out string text)
    {
        text = NumberText.Format(datum.Seconds) + "s";
        return true;
    }

    /// <summary>The one literal <paramref name="text"/> is, with nothing before or after it; null otherwise.</summary>
    private static Token? OnlyLiteral(string text) =>
        Lexer.Lex(text) is { Succeeded: true, Value: [var token, { Kind: TokenKind.EndOfText }] }
            && token.Span.Start == 0 && token.Span.End == text.Length
            ? token
            : null;

    private static bool Got<T>(T value, out T result)
    {
        result = value;
        return true;
    }
}
