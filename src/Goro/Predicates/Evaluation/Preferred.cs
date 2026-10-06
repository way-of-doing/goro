using System.Collections.Immutable;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// <c>PREFERRED(e1, e2, ...)</c>: the first argument holding a usable occurrence, taken entire; failing
/// that, the first that is not absent; failing that, absent. See <see cref="Preference"/>.
/// </summary>
public sealed class Preferred<T>(ImmutableArray<Expression<T>> arguments) : Expression<T> where T : notnull
{
    public ImmutableArray<Expression<T>> Arguments { get; } = arguments;

    public override bool IsDefinite => Arguments.All(argument => argument.IsDefinite);

    // Evaluates its arguments in written order and stops at the one chosen. It reads only the state of
    // their occurrences, so it reports nothing, and what it passes over is discarded unreported.
    public override Value<T> Evaluate(EvaluationContext context) =>
        Preference.Preferred(Arguments.Select(argument => argument.Evaluate(context)));
}
