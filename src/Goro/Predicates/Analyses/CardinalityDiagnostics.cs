using Goro.Messages;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Predicates.Analyses;

/// <summary>The errors of the cardinality analysis, and the rewrites it offers for them.</summary>
internal sealed class CardinalityDiagnostics(string text) : SemanticDiagnostics(text)
{
    public Diagnostic ConditionNotExactlyOne(ExpressionSyntax syntax) =>
        new(Codes.ConditionNotExactlyOne, syntax.Span, new ErrorMessage.ConditionNotExactlyOne(Code(syntax)),
            [new Suggestion(syntax.Span, $"{Text(syntax)} == TRUE"), new Suggestion(syntax.Span, $"{Text(syntax)} == FALSE")]);

    /// <summary>
    /// <c>genre != "metal"</c>: offers <c>NOT genre == "metal"</c> and <c>ALL(genre) != "metal"</c>,
    /// the two readings predicates.md says are usually meant.
    /// </summary>
    public Diagnostic AmbiguousNotEqual(ComparisonSyntax comparison, ExpressionSyntax? leftCore, ExpressionSyntax? rightCore)
    {
        var (left, right) = (comparison.Left, comparison.Right);
        var negated = "NOT "
            + Text(TextSpan.FromBounds(left.Span.Start, comparison.OperatorSpan.Start))
            + "=="
            + Text(TextSpan.FromBounds(comparison.OperatorSpan.End, right.Span.End));
        var universal = Universal(left, leftCore is not null)
            + Text(TextSpan.FromBounds(left.Span.End, right.Span.Start))
            + Universal(right, rightCore is not null);
        ErrorMessage message = (leftCore, rightCore) switch
        {
            ({ } l, { } r) => new ErrorMessage.AmbiguousNotEqualBoth(Code(l), Code(r)),
            ({ } l, null) => new ErrorMessage.AmbiguousNotEqual(Code(l)),
            (null, { } r) => new ErrorMessage.AmbiguousNotEqual(Code(r)),
            _ => throw new ArgumentException("Some operand must need a quantifier."),
        };
        return new Diagnostic(Codes.AmbiguousNotEqual, comparison.OperatorSpan, message,
            [new Suggestion(comparison.Span, negated), new Suggestion(comparison.Span, universal)]);
    }

    /// <summary>
    /// <c>id3v1::genre != "blues"</c>, where every operand needing a quantifier can be absent but never
    /// holds several occurrences: offers <c>NOT id3v1::genre == "blues"</c> and
    /// <c>FALLBACK(id3v1::genre, "") != "blues"</c>, the default being the empty value of the type.
    /// </summary>
    public Diagnostic MayBeAbsentNotEqual(ComparisonSyntax comparison, ExpressionSyntax? leftCore, ExpressionSyntax? rightCore, GoroType type)
    {
        var (left, right) = (comparison.Left, comparison.Right);
        var negated = "NOT "
            + Text(TextSpan.FromBounds(left.Span.Start, comparison.OperatorSpan.Start))
            + "=="
            + Text(TextSpan.FromBounds(comparison.OperatorSpan.End, right.Span.End));
        var settled = WithFallback(left, leftCore, type)
            + Text(TextSpan.FromBounds(left.Span.End, right.Span.Start))
            + WithFallback(right, rightCore, type);
        ErrorMessage message = (leftCore, rightCore) switch
        {
            ({ } l, { } r) => new ErrorMessage.MayBeAbsentNotEqualBoth(Code(l), Code(r)),
            ({ } l, null) => new ErrorMessage.MayBeAbsentNotEqual(Code(l)),
            (null, { } r) => new ErrorMessage.MayBeAbsentNotEqual(Code(r)),
            _ => throw new ArgumentException("Some operand must need deciding."),
        };
        return new Diagnostic(Codes.AmbiguousNotEqual, comparison.OperatorSpan, message,
            [new Suggestion(comparison.Span, negated), new Suggestion(comparison.Span, settled)]);
    }

    private string Universal(ExpressionSyntax operand, bool wrap) => wrap ? $"ALL({Text(operand)})" : Text(operand);

    /// <summary>
    /// The operand with its core wrapped in <c>FALLBACK()</c>, any modifiers staying outside it, where
    /// a modifier has to be; the operand unchanged when it has no core to wrap.
    /// </summary>
    private string WithFallback(ExpressionSyntax operand, ExpressionSyntax? core, GoroType type) =>
        core is null
            ? Text(operand)
            : Text(TextSpan.FromBounds(operand.Span.Start, core.Span.Start))
                + $"FALLBACK({Text(core)}, {EmptyOf(type)})"
                + Text(TextSpan.FromBounds(core.Span.End, operand.Span.End));

    /// <summary>The literal an absent value is most naturally compared as.</summary>
    private static string EmptyOf(GoroType type) => type switch
    {
        GoroType.String => "\"\"",
        GoroType.Number => "0",
        GoroType.ByteCount => "0b",
        GoroType.Duration => "0s",
        GoroType.Boolean => "FALSE",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a type with values."),
    };
}
