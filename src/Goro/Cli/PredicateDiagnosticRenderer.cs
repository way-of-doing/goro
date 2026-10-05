using System.Globalization;
using Goro.Messages;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;

namespace Goro.Cli;

/// <summary>
/// Writes the errors found in a predicate for a person to read: for each one, its message, the
/// predicate echoed with the offending span marked beneath it, and every suggested rewrite as the
/// whole predicate rewritten, ready to copy.
/// </summary>
/// <example>
/// <code>
/// goro: error: We compare with `==`, goro!
///   file::name = "a.mp3"
///              ^
///   try: file::name == "a.mp3"
/// </code>
/// </example>
/// <remarks>
/// <para>
/// The predicate is echoed on one line, whatever it holds: a control character, such as a line break
/// or a tab a shell let through, is shown as a space. Each is one UTF-16 unit replaced by one, so
/// spans still index the echo exactly as they index the text.
/// </para>
/// <para>
/// The marker is aligned by counting grapheme clusters (text elements), on the assumption that each
/// takes one column. That is right for a character followed by combining marks and for a character
/// written as a surrogate pair, and wrong for the characters a terminal draws two columns wide --
/// East Asian wide and fullwidth forms, and emoji -- after which the marker sits too far left. Doing
/// better needs the East Asian Width property, which .NET does not expose, and a predicate is
/// written mostly in ASCII, so this is accepted.
/// </para>
/// </remarks>
public static class PredicateDiagnosticRenderer
{
    private const string Indent = "  ";

    public static void Write(TextWriter writer, IErrorMessages messages, string text, IEnumerable<Diagnostic> diagnostics)
    {
        var echo = OneLine(text);
        var columns = StringInfo.ParseCombiningCharacters(echo);

        foreach (var diagnostic in diagnostics)
        {
            writer.WriteLine($"{messages.ErrorPrefix}{messages.Render(diagnostic.Message)}");

            // An empty predicate has nothing to echo, and nothing in it to point at.
            if (echo.Length > 0)
            {
                writer.WriteLine($"{Indent}{echo}");
                writer.WriteLine($"{Indent}{Marker(echo.Length, columns, diagnostic.Span)}");
            }

            foreach (var suggestion in diagnostic.Suggestions)
            {
                writer.WriteLine($"{Indent}{messages.SuggestionLabel}{OneLine(suggestion.ApplyTo(text))}");
            }
        }
    }

    /// <param name="starts">The offset at which each text element of the echo begins.</param>
    private static string Marker(int length, int[] starts, TextSpan span)
    {
        // A span starting inside a text element is drawn from that element, and one ending inside
        // one is drawn to its end; an empty span is drawn as one mark where it falls.
        var first = span.Start >= length ? starts.Length : starts.Count(start => start <= span.Start) - 1;
        var end = starts.Count(start => start < span.End);
        return new string(' ', first) + new string('^', Math.Max(1, end - first));
    }

    private static string OneLine(string text) =>
        string.Create(text.Length, text, static (buffer, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                buffer[i] = IsLineBreaking(c) ? ' ' : c;
            }
        });

    // Control characters include the line feed, carriage return, tab and NEL; the line and paragraph
    // separators are the two other characters a terminal may break a line at.
    private static bool IsLineBreaking(char c) =>
        char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
