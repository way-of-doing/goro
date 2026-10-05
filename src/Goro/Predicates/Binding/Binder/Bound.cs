// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using System.Globalization;
using System.Text;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// What the binder knows about one sub-expression once it has been analysed.
/// </summary>
/// <param name="Type">Its Goro type, or null for the error type: something below it was wrong in a
/// way that leaves its type unknown, and every check that would involve it is skipped, so that one
/// mistake is reported once rather than at every operator it reaches.</param>
/// <param name="IsDefinite">Whether it has exactly one occurrence for every file. The error type
/// counts as definite, which silences the checks that need definiteness.</param>
/// <param name="Node">The bound node, or null when some error below it, reported already, left
/// nothing to build it from. A tree with an error in it is never handed on, so null is harmless.</param>
/// <param name="Literal">The literal it was written as, looking through parentheses, if it was one.</param>
/// <param name="Shape">Its canonical form, which is what warning sources are interned by.</param>
internal sealed record Bound(GoroType? Type, bool IsDefinite, BoundExpression? Node, LiteralSyntax? Literal, Shape Shape)
{
    public bool IsError => Type is null;

    public static Bound Error { get; } = new(null, true, null, null, Shape.Unknown);

    /// <summary>A number literal, which is what may stand for a bytecount or a duration.</summary>
    public bool IsNumberLiteral => Type == GoroType.Number && Literal?.Token is NumberToken;
}

/// <summary>
/// The structure of a sub-expression with everything about how it was written erased: whitespace,
/// case, parentheses, modifiers, and which string form wrote a literal.
/// </summary>
/// <param name="Key">What identity is decided by. Identifiers appear in it as the number the
/// interner gave them, so it is exactly as fine as <c>IdentifierName</c>'s own equality.</param>
/// <param name="Form">The same structure for a reader, as the source table lists it.</param>
internal readonly record struct Shape(string Key, string Form)
{
    public static Shape Unknown { get; } = new("?", "?");

    /// <summary>A function applied to arguments, such as <c>NUMBER(x)</c>.</summary>
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
