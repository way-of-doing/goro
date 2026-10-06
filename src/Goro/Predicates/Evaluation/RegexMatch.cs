// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using System.Text.RegularExpressions;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// <c>subject =~ pattern</c>. The subject is prepared in <see cref="Mode"/>; the pattern never is.
/// The patterns analysis compiles <see cref="Pattern"/> with the non-backtracking engine, culture-invariant,
/// and case-insensitive in normalized mode.
/// </summary>
public sealed class RegexMatch(Operand<string> subject, Regex pattern, ComparisonMode mode) : Condition
{
    public Operand<string> Subject { get; } = subject;

    public Regex Pattern { get; } = pattern;

    public ComparisonMode Mode { get; } = mode;

    public override Truth Decide(EvaluationContext context) =>
        OperatorEvaluation.Decide<string, Condition>(
            context, [new(Subject.Expression.Evaluate(context), Subject.Quantifier)], new(Pattern, Mode));

    /// <summary>The subject is prepared like any string an operator reads; the pattern never is.</summary>
    private readonly struct Condition(Regex pattern, ComparisonMode mode) : IOperatorCondition<string>
    {
        public string Prepare(string datum) => Normalization.Prepare(datum, mode);

        public bool HoldsOf(ReadOnlySpan<string> data) => pattern.IsMatch(data[0]);
    }
}
