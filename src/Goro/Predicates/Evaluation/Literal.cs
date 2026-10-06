// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>A literal: one usable occurrence, the same for every file.</summary>
public sealed class Literal<T>(T value) : Expression<T> where T : notnull
{
    private readonly Value<T> occurrence = Value<T>.Single(new Usable<T>(value));

    public T Value { get; } = value;

    public override bool IsDefinite => true;

    public override Value<T> Evaluate(EvaluationContext context) => occurrence;
}
