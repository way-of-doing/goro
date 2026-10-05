// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary><c>FALLBACK(e, d)</c>: substitutes <see cref="Default"/> for absence and for each unusable occurrence.</summary>
public sealed class Fallback<T>(BoundExpression<T> argument, T @default) : BoundExpression<T> where T : notnull
{
    public BoundExpression<T> Argument { get; } = argument;

    public T Default { get; } = @default;

    public override bool IsDefinite => Argument.IsDefinite;

    public override Value<T> Evaluate(EvaluationContext context) => throw new NotImplementedException();
}
