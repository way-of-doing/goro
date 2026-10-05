// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary><c>FALLBACK(e, d)</c>: substitutes <see cref="Default"/> for absence and for each unusable occurrence.</summary>
public sealed class Fallback<T>(BoundExpression<T> argument, T @default) : BoundExpression<T> where T : notnull
{
    public BoundExpression<T> Argument { get; } = argument;

    private readonly Usable<T> substitute = new(@default);

    public T Default { get; } = @default;

    public override bool IsDefinite => Argument.IsDefinite;

    // Substitutes rather than reads, so it reports nothing.
    public override Value<T> Evaluate(EvaluationContext context)
    {
        var value = Argument.Evaluate(context);
        if (value.IsAbsent)
        {
            return Value<T>.Single(substitute);
        }

        var occurrences = value.Occurrences;
        if (!occurrences.Any(occurrence => occurrence is Unusable<T>))
        {
            return value;
        }

        return Value<T>.Of(occurrences.Select(occurrence => occurrence is Unusable<T> ? substitute : occurrence));
    }
}
