using System.Globalization;

namespace Goro.Predicates.Identifiers.Interpretation;

/// <summary>
/// Dates out of text: see "Parsing dates" in docs/features/builtins/identifiers.md. The accepted
/// forms are the timestamp formats Id3v2 defines as its subset of ISO 8601, and they are accepted
/// wherever date-shaped data is read.
/// </summary>
public static class DateShapes
{
    /// <summary>
    /// The year of an already trimmed value in one of the six forms, <c>YYYY</c> to
    /// <c>YYYY-MM-DDTHH:MM:SS</c>, every field but the year in exactly two digits. The value must
    /// denote a real Gregorian date, whose calendar has no year zero. Null where it does not, which
    /// makes it an unusable occurrence.
    /// </summary>
    public static decimal? Year(string trimmed)
    {
        // The forms differ only in how far they go, so their lengths say which one a value is in.
        if (trimmed.Length is not (4 or 7 or 10 or 13 or 16 or 19))
        {
            return null;
        }

        var year = Number(trimmed, 0, 4);
        var month = trimmed.Length >= 7 ? Field(trimmed, 4, '-') : 1;
        var day = trimmed.Length >= 10 ? Field(trimmed, 7, '-') : 1;
        var hour = trimmed.Length >= 13 ? Field(trimmed, 10, 'T') : 0;
        var minute = trimmed.Length >= 16 ? Field(trimmed, 13, ':') : 0;
        var second = trimmed.Length >= 19 ? Field(trimmed, 16, ':') : 0;
        return year is >= 1 and <= 9999
            && month is >= 1 and <= 12
            && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            && hour is >= 0 and <= 23
            && minute is >= 0 and <= 59
            && second is >= 0 and <= 59
                ? year
                : null;
    }

    /// <summary>A two-digit field after its separator at <paramref name="at"/>, or -1.</summary>
    private static int Field(string text, int at, char separator) =>
        at < text.Length && text[at] == separator ? Number(text, at + 1, 2) : -1;

    /// <summary>
    /// The <paramref name="length"/> characters at <paramref name="at"/> as a number, or -1 where they
    /// are not all ASCII digits, run past the end of the text, or make a number too large for an int.
    /// </summary>
    private static int Number(string text, int at, int length) =>
        at >= 0 && length > 0 && at <= text.Length - length
        && int.TryParse(text.AsSpan(at, length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : -1;
}
