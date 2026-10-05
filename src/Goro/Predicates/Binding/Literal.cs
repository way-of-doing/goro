// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>A literal: one usable occurrence, the same for every file.</summary>
public sealed class Literal<T>(T value) : BoundExpression<T> where T : notnull
{
    public T Value { get; } = value;

    public override bool IsDefinite => true;

    public override Value<T> Evaluate(EvaluationContext context) => throw new NotImplementedException();
}
