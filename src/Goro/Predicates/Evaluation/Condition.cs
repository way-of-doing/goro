using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// An operator: a comparison, range, regex, state test or logical operator. Every one produces
/// exactly one boolean, so it is definite, and is evaluated directly to a <see cref="Truth"/>.
/// </summary>
public abstract class Condition : Expression<bool>
{
    public sealed override bool IsDefinite => true;

    public abstract Truth Decide(EvaluationContext context);

    public sealed override Value<bool> Evaluate(EvaluationContext context) => Decide(context).ToValue();
}

public static class BooleanExtensions
{
    /// <summary>The truth of a definite boolean expression, whether or not it is an operator.</summary>
    public static Truth Decide(this Expression<bool> expression, EvaluationContext context) =>
        expression is Condition condition ? condition.Decide(context) : expression.Evaluate(context).ToTruth();
}
