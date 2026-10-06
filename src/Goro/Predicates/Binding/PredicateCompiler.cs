using Goro.Predicates.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;

namespace Goro.Predicates.Binding;

/// <summary>Reads a predicate from its text to something that can be evaluated.</summary>
/// <remarks>
/// The stages: the lexer and the parser, which stop at the first error; the binder, which
/// establishes what the predicate means; the analyses, each owning one property and the rules that
/// read it, which need the binder's types but not one another; and, for a predicate none of them
/// found anything wrong with, lowering to the tree that is evaluated.
/// </remarks>
public static class PredicateCompiler
{
    public static StageResult<CompiledPredicate> Compile(string text, IIdentifierCatalog catalog) =>
        SyntaxTree.Parse(text).Then(tree => Compile(tree, catalog));

    private static StageResult<CompiledPredicate> Compile(SyntaxTree syntax, IIdentifierCatalog catalog)
    {
        // Every analysis runs on a tree with errors in it too, so that every independent error is
        // reported in one run.
        var tree = Binder.Bind(syntax, catalog);
        var cardinality = Cardinality.Analyse(tree);
        var constants = Constants.Analyse(tree);
        var patterns = Patterns.Analyse(tree);

        var diagnostics = tree.Diagnostics
            .Concat(cardinality.Diagnostics)
            .Concat(constants.Diagnostics)
            .Concat(patterns.Diagnostics)
            .ToList();
        if (diagnostics.Count > 0)
        {
            // In text order; of errors that start together, the one about the smaller part first,
            // as a reader working outwards from the mistake would meet them.
            return StageResult<CompiledPredicate>.Failure(diagnostics.OrderBy(d => d.Span.Start).ThenBy(d => d.Span.End));
        }

        return StageResult<CompiledPredicate>.Success(Lowering.Lower(tree, constants, patterns, Sources.Analyse(tree)));
    }
}
