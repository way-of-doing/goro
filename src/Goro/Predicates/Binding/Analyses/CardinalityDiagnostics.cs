using Goro.Messages;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Predicates.Binding;

/// <summary>The errors of the cardinality analysis, and the rewrites it offers for them.</summary>
internal sealed class CardinalityDiagnostics(string text) : SemanticDiagnostics(text)
{
    public Diagnostic IndefiniteCondition(ExpressionSyntax syntax) =>
        new(Codes.IndefiniteCondition, syntax.Span, new ErrorMessage.IndefiniteCondition(Code(syntax)),
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

    private string Universal(ExpressionSyntax operand, bool wrap) => wrap ? $"ALL({Text(operand)})" : Text(operand);
}
