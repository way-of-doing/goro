using System.Globalization;

namespace Goro.Predicates.Identifiers.Interpretation;

/// <summary>
/// Track numbers out of text: see "Parsing track numbers" in docs/features/builtins/identifiers.md.
/// </summary>
public static class TrackNumbers
{
    /// <summary>
    /// The track an already trimmed value denotes: the ASCII decimal notation of an unsigned integer,
    /// or of two separated by a slash with no whitespace, of which the first is the track. Null where
    /// the value is neither, an empty one included, which makes it an unusable occurrence.
    /// </summary>
    public static decimal? Parse(string trimmed)
    {
        var slash = trimmed.IndexOf('/');
        if (slash < 0)
        {
            return Unsigned(trimmed);
        }

        return Unsigned(trimmed[(slash + 1)..]) is not null ? Unsigned(trimmed[..slash]) : null;
    }

    /// <summary>Digits only, as many as there are, as long as the number fits a decimal.</summary>
    private static decimal? Unsigned(string digits) =>
        digits.Length > 0 && digits.All(char.IsAsciiDigit)
        && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
}
