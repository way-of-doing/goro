using System.Globalization;
using System.Text;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Analyses;

/// <summary>
/// The structure of a sub-expression with everything about how it was written erased: whitespace,
/// case, parentheses, modifiers, and which string form wrote a literal.
/// </summary>
/// <param name="Key">What identity is decided by. What is read from the file appears in it as the number the
/// interner gave it, so it is exactly as fine as <c>DeclaredName</c>'s own equality.</param>
/// <param name="Form">The same structure for a reader, as the source table lists it.</param>
internal readonly record struct Shape(string Key, string Form)
{
    /// <summary>A conversion, such as <c>x AS NUMBER</c>.</summary>
    public static Shape Conversion(Shape operand, GoroType target)
    {
        var name = target.ToString().ToUpperInvariant();
        return new($"(AS {operand.Key} {name})", $"{operand.Form} AS {name}");
    }

    /// <summary>A function applied to arguments, such as <c>COUNT(x)</c>.</summary>
    public static Shape Call(string function, params Shape[] arguments) =>
        new($"{function}({string.Join(",", arguments.Select(a => a.Key))})",
            $"{function}({string.Join(", ", arguments.Select(a => a.Form))})");

    /// <summary>
    /// An operator. The key is fully parenthesized, so that it is unambiguous; the form is written
    /// as a predicate would be, for reading.
    /// </summary>
    public static Shape Operator(string symbol, Shape left, string right) =>
        new($"({symbol} {left.Key} {right})", $"{left.Form} {symbol} {right}");

    public static Shape Operator(string symbol, Shape left, Shape right) =>
        new($"({symbol} {left.Key} {right.Key})", $"{left.Form} {symbol} {right.Form}");

    public static Shape Not(Shape operand) => new($"(NOT {operand.Key})", $"NOT {operand.Form}");

    public static Shape OfLiteral(Token token) => token switch
    {
        StringToken text => Text(Quote(text.Value)),
        NumberToken number => Text(NumberText.Format(number.Value)),
        ByteCountToken bytes => OfUnit(GoroType.ByteCount, bytes.Value.Bytes),
        DurationToken duration => OfUnit(GoroType.Duration, duration.Value.Seconds),
        { Kind: TokenKind.True } => Text("TRUE"),
        { Kind: TokenKind.False } => Text("FALSE"),
        _ => throw new ArgumentOutOfRangeException(nameof(token), token.Kind, "Not a literal."),
    };

    /// <summary>A bytecount in bytes or a duration in seconds, however it was written.</summary>
    public static Shape OfUnit(GoroType type, decimal value) =>
        Text(NumberText.Format(value) + (type == GoroType.ByteCount ? "b" : "s"));

    /// <summary>A string as a quoted string literal, so that both forms of one string agree.</summary>
    public static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(c) =>"\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }

    private static Shape Text(string text) => new(text, text);
}
