using Goro.Predicates.Binding;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;

namespace Goro.Tests.Predicates.Support;

/// <summary>
/// Reads predicates from text as far as the semantic tree, for tests of the binder, of one analysis,
/// or of lowering, each of which can then be run on its own.
/// </summary>
internal static class Analyse
{
    public static SemanticTree Bind(string text, IIdentifierCatalog? catalog = null)
    {
        var parsed = SyntaxTree.Parse(text);
        Assert.That(parsed.Succeeded, Is.True, () => $"`{text}` did not parse: {CompileAssert.Describe(parsed.Diagnostics)}");
        return Binder.Bind(parsed.Value, catalog ?? TestCatalog.Standard());
    }

    /// <summary>The one node of the tree that was written as <paramref name="written"/>.</summary>
    public static SemanticExpression Find(this SemanticTree tree, string written) =>
        tree.Nodes.Single(node => node.Syntax.Span.Of(tree.Text) == written);

    public static T Find<T>(this SemanticTree tree, string written) where T : SemanticExpression =>
        (T)tree.Find(written);

    /// <summary>The codes of a stage's errors, in the order it reported them.</summary>
    public static string[] Codes(this IEnumerable<Goro.Predicates.Diagnostics.Diagnostic> diagnostics) =>
        [.. diagnostics.Select(d => d.Code)];
}
