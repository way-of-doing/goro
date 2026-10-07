using System.Globalization;
using System.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Syntax;

/// <summary>
/// Writes a syntax tree as an S-expression, which shows its shape at a glance:
/// <c>artist == "metallica" AND NOT year &gt; 2000</c> is <c>(AND (== artist "metallica") (NOT (&gt; year 2000)))</c>.
/// </summary>
/// <remarks>
/// Operators and modifiers are written in upper case, a function call as <c>(call NAME args...)</c>
/// and parentheses as <c>(paren expr)</c>. Names keep their spelling. Every literal is written as a
/// literal that reads back to the same value: numbers in canonical form, bytecounts in bytes and
/// durations in seconds, and strings in the form they were written in.
/// </remarks>
public static class SyntaxPrinter
{
    public static string Print(SyntaxNode node) => node switch
    {
        LiteralSyntax literal => Literal(literal.Token),
        NullSyntax => "NULL",
        IdentifierSyntax identifier => Qualified(identifier.Source, identifier.Name),
        FunctionCallSyntax call => List(["call", Qualified(call.Source, call.Name), .. call.Arguments.Select(Print)]),
        ParenthesizedSyntax parentheses => List("paren", Print(parentheses.Expression)),
        ModifierSyntax modifier => List(modifier.Modifier.ToString().ToUpperInvariant(), Print(modifier.Operand)),
        AsSyntax conversion => List("AS", Print(conversion.Operand), conversion.Target.Text),
        ComparisonSyntax comparison => List(Operator(comparison.Operator), Print(comparison.Left), Print(comparison.Right)),
        BetweenSyntax between => List("BETWEEN", Print(between.Subject), Print(between.Minimum), Print(between.Maximum)),
        MatchSyntax match => List("=~", Print(match.Subject), Literal(match.Pattern)),
        StateTestSyntax test => List("IS", Print(test.Operand), test.State.ToString().ToUpperInvariant()),
        NotSyntax not => List("NOT", Print(not.Operand)),
        LogicalSyntax logical => List(logical.Operator.ToString().ToUpperInvariant(), Print(logical.Left), Print(logical.Right)),
        _ => throw new ArgumentOutOfRangeException(nameof(node), node.GetType().Name, "Not a syntax node the printer knows."),
    };

    private static string List(params string[] items) => $"({string.Join(' ', items)})";

    private static string Qualified(NameToken? source, NameToken name) => source is null ? name.Text : $"{source.Text}::{name.Text}";

    private static string Operator(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => "==",
        ComparisonOperator.NotEqual => "!=",
        ComparisonOperator.Less => "<",
        ComparisonOperator.LessOrEqual => "<=",
        ComparisonOperator.Greater => ">",
        ComparisonOperator.GreaterOrEqual => ">=",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private static string Literal(Token token) => token switch
    {
        StringToken { IsRaw: true } raw => $"r\"{raw.Value.Replace("\"", "\"\"")}\"",
        StringToken quoted => Quoted(quoted.Value),
        NumberToken number => NumberText.Format(number.Value),
        ByteCountToken bytes => NumberText.Format(bytes.Value.Bytes) + "b",
        DurationToken duration => NumberText.Format(duration.Value.Seconds) + "s",
        { Kind: TokenKind.True } => "TRUE",
        { Kind: TokenKind.False } => "FALSE",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token.Kind, "Not a literal."),
    };

    private static string Quoted(string value)
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
                _ when char.IsControl(c) => "\\x" + ((int)c).ToString("X2", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }
}
