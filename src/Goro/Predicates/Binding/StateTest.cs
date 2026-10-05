// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// <c>e IS state</c>. Reads cardinality and state, never content, so it never reports anything.
/// <see cref="Quantifier"/> is ignored for <see cref="TestedState.Absent"/>, which asks about a whole value.
/// </summary>
public sealed class StateTest<T>(BoundExpression<T> operand, Quantifier quantifier, TestedState state)
    : BoundCondition where T : notnull
{
    public BoundExpression<T> Operand { get; } = operand;

    public Quantifier Quantifier { get; } = quantifier;

    public TestedState State { get; } = state;

    public override Truth Decide(EvaluationContext context) => throw new NotImplementedException();
}
