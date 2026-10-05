// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// <c>subject BETWEEN min..max</c>, tested once per occurrence. The binder hands over the endpoints
/// already prepared by <see cref="Order"/>, having checked that the range is not reversed. Each
/// endpoint is compared with a datum by <see cref="DatumOrder{T}.CompareWithEndpoint"/>, which for
/// strings makes the bounds prefixes.
/// </summary>
public sealed class RangeTest<T>(BoundOperand<T> subject, T minimum, T maximum, DatumOrder<T> order)
    : BoundCondition where T : notnull
{
    public BoundOperand<T> Subject { get; } = subject;

    public T Minimum { get; } = minimum;

    public T Maximum { get; } = maximum;

    public DatumOrder<T> Order { get; } = order;

    public override Truth Decide(EvaluationContext context) =>
        OperatorEvaluation.Decide<T, Condition>(
            context, [new(Subject.Expression.Evaluate(context), Subject.Quantifier)], new(Minimum, Maximum, Order));

    /// <summary>
    /// Both bounds are tested of the same occurrence, within one quantifier scope: this is one
    /// operator, not a pair of comparisons. The endpoints are already prepared, so only the
    /// subject's data are.
    /// </summary>
    private readonly struct Condition(T minimum, T maximum, DatumOrder<T> order) : IOperatorCondition<T>
    {
        public T Prepare(T datum) => order.Prepare(datum);

        public bool HoldsOf(ReadOnlySpan<T> data) =>
            order.CompareWithEndpoint(data[0], minimum) >= 0 && order.CompareWithEndpoint(data[0], maximum) <= 0;
    }
}
