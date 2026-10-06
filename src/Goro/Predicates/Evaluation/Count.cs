// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary><c>COUNT(e)</c>: the number of occurrences, unusable ones included; never warns.</summary>
public sealed class Count<T>(Expression<T> argument) : Expression<decimal> where T : notnull
{
    public Expression<T> Argument { get; } = argument;

    public override Bounds Bounds => Bounds.ExactlyOne;

    // Reads cardinality only, so it asks nothing of any occurrence and reports nothing.
    public override Value<decimal> Evaluate(EvaluationContext context) =>
        Value<decimal>.Single(new Usable<decimal>(Argument.Evaluate(context).Occurrences.Length));
}
