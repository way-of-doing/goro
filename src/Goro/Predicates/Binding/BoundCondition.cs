using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// An operator: a comparison, range, regex, state test or logical operator. Every one produces
/// exactly one boolean, so it is definite, and is evaluated directly to a <see cref="Truth"/>.
/// </summary>
public abstract class BoundCondition : BoundExpression<bool>
{
    public sealed override bool IsDefinite => true;

    public abstract Truth Decide(EvaluationContext context);

    public sealed override Value<bool> Evaluate(EvaluationContext context) => Decide(context).ToValue();
}

public static class BoundBooleanExtensions
{
    /// <summary>The truth of a definite boolean expression, whether or not it is an operator.</summary>
    public static Truth Decide(this BoundExpression<bool> expression, EvaluationContext context) =>
        expression is BoundCondition condition ? condition.Decide(context) : expression.Evaluate(context).ToTruth();
}
