// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using System.Diagnostics;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// A comparison. <see cref="Order"/> already embodies the operator's comparison mode, so for strings
/// it is the normalized or the literal order.
/// </summary>
public sealed class ComparisonTest<T>(BoundOperand<T> left, ComparisonOperator @operator, BoundOperand<T> right, DatumOrder<T> order)
    : BoundCondition where T : notnull
{
    public BoundOperand<T> Left { get; } = left;

    public ComparisonOperator Operator { get; } = @operator;

    public BoundOperand<T> Right { get; } = right;

    public DatumOrder<T> Order { get; } = order;

    public override Truth Decide(EvaluationContext context) =>
        OperatorEvaluation.Decide<T, Condition>(
            context,
            [new(Left.Expression.Evaluate(context), Left.Quantifier), new(Right.Expression.Evaluate(context), Right.Quantifier)],
            new(Operator, Order));

    private readonly struct Condition(ComparisonOperator @operator, DatumOrder<T> order) : IOperatorCondition<T>
    {
        public T Prepare(T datum) => order.Prepare(datum);

        public bool HoldsOf(ReadOnlySpan<T> data)
        {
            var comparison = order.Compare(data[0], data[1]);
            return @operator switch
            {
                ComparisonOperator.Equal => comparison == 0,
                ComparisonOperator.NotEqual => comparison != 0,
                ComparisonOperator.Less => comparison < 0,
                ComparisonOperator.LessOrEqual => comparison <= 0,
                ComparisonOperator.Greater => comparison > 0,
                ComparisonOperator.GreaterOrEqual => comparison >= 0,
                _ => throw new UnreachableException(),
            };
        }
    }
}
