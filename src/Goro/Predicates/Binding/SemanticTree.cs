using System.Collections.Immutable;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;

namespace Goro.Predicates.Binding;

/// <summary>
/// A predicate as the binder understood it: what each part means, and the errors found in doing
/// so. A tree with errors in it is still a whole tree, an error node standing wherever something
/// could not be made sense of, so that every analysis can still look for mistakes of its own.
/// </summary>
/// <param name="Syntax">The syntax tree it was bound from.</param>
/// <param name="Root">The predicate, which the specification requires to be a definite boolean.</param>
/// <param name="Diagnostics">The binder's own errors.</param>
public sealed record SemanticTree(SyntaxTree Syntax, SemanticExpression Root, ImmutableArray<Diagnostic> Diagnostics)
{
    public string Text => Syntax.Text;

    /// <summary>Every node of the tree, children before their parents, in text order.</summary>
    public IEnumerable<SemanticExpression> Nodes => Root.DescendantsAndSelf();
}
