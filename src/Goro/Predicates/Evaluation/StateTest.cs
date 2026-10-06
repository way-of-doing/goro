// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// <c>e IS state</c>. Reads cardinality and state, never content, so it never reports anything.
/// <see cref="Quantifier"/> is ignored for <see cref="TestedState.Absent"/>, which asks about a whole value.
/// </summary>
public sealed class StateTest<T>(Expression<T> operand, Quantifier quantifier, TestedState state)
    : Condition where T : notnull
{
    public Expression<T> Operand { get; } = operand;

    public Quantifier Quantifier { get; } = quantifier;

    public TestedState State { get; } = state;

    // Reads cardinality and state only, so it reports nothing, and is never unusable.
    public override Truth Decide(EvaluationContext context)
    {
        var value = Operand.Evaluate(context);
        if (State == TestedState.Absent)
        {
            return Truths.Of(value.IsAbsent);
        }

        // Like every operator other than the logical ones, false for an absent operand: so
        // ALL(x) IS USABLE is not vacuously true.
        if (value.IsAbsent)
        {
            return Truth.False;
        }

        var outcomes = new Quantification(Quantifier);
        foreach (var occurrence in value.Occurrences)
        {
            var usable = occurrence is Usable<T>;
            outcomes.Add(Truths.Of(State == TestedState.Usable ? usable : !usable));
        }

        return outcomes.Result;
    }
}
