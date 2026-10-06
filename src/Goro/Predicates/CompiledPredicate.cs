using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates;

/// <summary>
/// A predicate that has been read and checked, ready to be evaluated against any number of files
/// concurrently. Nothing about a single file is stored on it.
/// </summary>
public sealed class CompiledPredicate
{
    public CompiledPredicate(string text, Expression<bool> root, SourceTable sources)
    {
        if (root.Bounds != Bounds.ExactlyOne)
        {
            throw new ArgumentException("A predicate must be exactly one boolean.", nameof(root));
        }

        Text = text;
        Root = root;
        Sources = sources;
    }

    public string Text { get; }

    public Expression<bool> Root { get; }

    public SourceTable Sources { get; }

    /// <exception cref="Identifiers.UnreadableFileException">The file could not be read as far as the predicate needed.</exception>
    public Truth Evaluate(EvaluationContext context) => Root.Decide(context);
}
