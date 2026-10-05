// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using System.Text.RegularExpressions;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// <c>subject =~ pattern</c>. The subject is prepared in <see cref="Mode"/>; the pattern never is.
/// The binder compiles <see cref="Pattern"/> with the non-backtracking engine, culture-invariant,
/// and case-insensitive in normalized mode.
/// </summary>
public sealed class RegexMatch(BoundOperand<string> subject, Regex pattern, ComparisonMode mode) : BoundCondition
{
    public BoundOperand<string> Subject { get; } = subject;

    public Regex Pattern { get; } = pattern;

    public ComparisonMode Mode { get; } = mode;

    public override Truth Decide(EvaluationContext context) => throw new NotImplementedException();
}
