// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// A comparison. <see cref="Order"/> already embodies the operator's comparison mode, so for strings
/// it is the normalized or the literal order.
/// </summary>
public sealed class Comparison<T>(BoundOperand<T> left, ComparisonOperator @operator, BoundOperand<T> right, DatumOrder<T> order)
    : BoundCondition where T : notnull
{
    public BoundOperand<T> Left { get; } = left;

    public ComparisonOperator Operator { get; } = @operator;

    public BoundOperand<T> Right { get; } = right;

    public DatumOrder<T> Order { get; } = order;

    public override Truth Decide(EvaluationContext context) => throw new NotImplementedException();
}
