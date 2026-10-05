// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary><c>COUNT(e)</c>: the number of occurrences, unusable ones included; never warns.</summary>
public sealed class Count<T>(BoundExpression<T> argument) : BoundExpression<decimal> where T : notnull
{
    public BoundExpression<T> Argument { get; } = argument;

    public override bool IsDefinite => true;

    public override Value<decimal> Evaluate(EvaluationContext context) => throw new NotImplementedException();
}
