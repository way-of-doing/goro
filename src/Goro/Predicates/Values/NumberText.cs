using System.Globalization;

namespace Goro.Predicates.Values;

/// <summary>
/// The text of a number, in both directions: reading the number literal syntax, which the lexer and
/// <c>NUMBER()</c> share, and writing the one canonical form <c>STRING()</c> produces.
/// </summary>
public static class NumberText
{
    /// <summary>
    /// Reads <c>[+|-] (digits [. digits] | . digits)</c>, and succeeds only if the value it denotes
    /// can be held exactly.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out decimal number)
    {
        number = 0;
        var negative = false;
        var rest = text;
        if (!rest.IsEmpty && rest[0] is '+' or '-')
        {
            negative = rest[0] == '-';
            rest = rest[1..];
        }

        var point = rest.IndexOf('.');
        var integerPart = point < 0 ? rest : rest[..point];
        var fractionPart = point < 0 ? [] : rest[(point + 1)..];
        if (!IsDigits(integerPart, allowEmpty: point >= 0) || !IsDigits(fractionPart, allowEmpty: point < 0))
        {
            return false;
        }

        if (!decimal.TryParse(rest, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var magnitude))
        {
            return false;
        }

        // decimal.TryParse rounds what it cannot hold; it is exact only if nothing was lost.
        var significantInteger = integerPart.TrimStart('0');
        var significantFraction = fractionPart.TrimEnd('0');
        var expected = significantFraction.IsEmpty
            ? (significantInteger.IsEmpty ? "0" : significantInteger.ToString())
            : $"{(significantInteger.IsEmpty ? "0" : significantInteger)}.{significantFraction}";
        if (Format(magnitude) != expected)
        {
            return false;
        }

        number = negative && magnitude != 0 ? -magnitude : magnitude;
        return true;
    }

    /// <summary>
    /// The canonical text of a number: an integer part always, a sign only when negative, no
    /// negative zero, no exponent, no thousands separator and no trailing zeros after the point.
    /// </summary>
    public static string Format(decimal number) =>
        number == 0 ? "0" : number.ToString("0.############################", CultureInfo.InvariantCulture);

    private static bool IsDigits(ReadOnlySpan<char> text, bool allowEmpty) =>
        text.IsEmpty ? allowEmpty : !text.ContainsAnyExcept("0123456789");
}
