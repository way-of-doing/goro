// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary><c>NOT a</c>.</summary>
public sealed class Not(BoundExpression<bool> operand) : BoundCondition
{
    public BoundExpression<bool> Operand { get; } = RequireDefinite(operand);

    public override Truth Decide(EvaluationContext context) => throw new NotImplementedException();

    internal static BoundExpression<bool> RequireDefinite(BoundExpression<bool> operand) =>
        operand.IsDefinite ? operand : throw new ArgumentException("A logical operand must be definite.", nameof(operand));
}

/// <summary><c>a AND b</c>, short-circuiting on a false <see cref="Left"/>.</summary>
public sealed class And(BoundExpression<bool> left, BoundExpression<bool> right) : BoundCondition
{
    public BoundExpression<bool> Left { get; } = Not.RequireDefinite(left);

    public BoundExpression<bool> Right { get; } = Not.RequireDefinite(right);

    public override Truth Decide(EvaluationContext context) => throw new NotImplementedException();
}

/// <summary><c>a OR b</c>, short-circuiting on a true <see cref="Left"/>.</summary>
public sealed class Or(BoundExpression<bool> left, BoundExpression<bool> right) : BoundCondition
{
    public BoundExpression<bool> Left { get; } = Not.RequireDefinite(left);

    public BoundExpression<bool> Right { get; } = Not.RequireDefinite(right);

    public override Truth Decide(EvaluationContext context) => throw new NotImplementedException();
}
