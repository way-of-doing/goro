using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// A predicate that has been read and checked, ready to be evaluated against any number of files
/// concurrently. Nothing about a single file is stored on it.
/// </summary>
public sealed class CompiledPredicate
{
    public CompiledPredicate(string text, BoundExpression<bool> root, SourceTable sources)
    {
        if (!root.IsDefinite)
        {
            throw new ArgumentException("A predicate must be a definite boolean.", nameof(root));
        }

        Text = text;
        Root = root;
        Sources = sources;
    }

    public string Text { get; }

    public BoundExpression<bool> Root { get; }

    public SourceTable Sources { get; }

    /// <exception cref="Identifiers.UnreadableFileException">The file could not be read as far as the predicate needed.</exception>
    public Truth Evaluate(EvaluationContext context) => Root.Decide(context);
}
