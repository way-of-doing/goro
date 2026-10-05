using Goro.Predicates.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;

namespace Goro.Predicates.Binding;

/// <summary>Reads a predicate from its text to something that can be evaluated.</summary>
public static class PredicateCompiler
{
    public static StageResult<CompiledPredicate> Compile(string text, IIdentifierCatalog catalog) =>
        SyntaxTree.Parse(text).Then(tree => Binder.Bind(tree, catalog));
}
