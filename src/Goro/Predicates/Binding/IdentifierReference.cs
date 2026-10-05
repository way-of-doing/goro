// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>A resolved identifier. Its unusable occurrences are born here, at <see cref="Origin"/>.</summary>
public sealed class IdentifierReference<T>(IdentifierDeclaration<T> declaration, Origin origin)
    : BoundExpression<T> where T : notnull
{
    public IdentifierDeclaration<T> Declaration { get; } = declaration;

    public Origin Origin { get; } = origin;

    public override bool IsDefinite => Declaration.IsDefinite;

    public override Value<T> Evaluate(EvaluationContext context) => Declaration.Binding.Resolve(context.File, Origin);
}
