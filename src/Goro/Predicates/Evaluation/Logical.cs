// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary><c>NOT a</c>.</summary>
public sealed class Not(Expression<bool> operand) : Condition
{
    public Expression<bool> Operand { get; } = RequireExactlyOne(operand);

    public override Truth Decide(EvaluationContext context) => Operand.Decide(context) switch
    {
        Truth.True => Truth.False,
        Truth.False => Truth.True,
        _ => Truth.Unusable,
    };

    internal static Expression<bool> RequireExactlyOne(Expression<bool> operand) =>
        operand.Bounds == Bounds.ExactlyOne ? operand : throw new ArgumentException("A logical operand must be exactly one boolean.", nameof(operand));
}

/// <summary><c>a AND b</c>, short-circuiting on a false <see cref="Left"/>.</summary>
public sealed class And(Expression<bool> left, Expression<bool> right) : Condition
{
    public Expression<bool> Left { get; } = Not.RequireExactlyOne(left);

    public Expression<bool> Right { get; } = Not.RequireExactlyOne(right);

    /// <remarks>
    /// Only a false <see cref="Left"/> settles the result; an unusable one does not, so the right
    /// operand is evaluated, and may report, even then.
    /// </remarks>
    public override Truth Decide(EvaluationContext context)
    {
        var left = Left.Decide(context);
        if (left == Truth.False)
        {
            return Truth.False;
        }

        var right = Right.Decide(context);
        return right == Truth.False ? Truth.False
            : left == Truth.Unusable || right == Truth.Unusable ? Truth.Unusable
            : Truth.True;
    }
}

/// <summary><c>a OR b</c>, short-circuiting on a true <see cref="Left"/>.</summary>
public sealed class Or(Expression<bool> left, Expression<bool> right) : Condition
{
    public Expression<bool> Left { get; } = Not.RequireExactlyOne(left);

    public Expression<bool> Right { get; } = Not.RequireExactlyOne(right);

    /// <remarks>
    /// Only a true <see cref="Left"/> settles the result; an unusable one does not, so the right
    /// operand is evaluated, and may report, even then.
    /// </remarks>
    public override Truth Decide(EvaluationContext context)
    {
        var left = Left.Decide(context);
        if (left == Truth.True)
        {
            return Truth.True;
        }

        var right = Right.Decide(context);
        return right == Truth.True ? Truth.True
            : left == Truth.Unusable || right == Truth.Unusable ? Truth.Unusable
            : Truth.False;
    }
}
