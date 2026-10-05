using System.Collections.Immutable;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Tests.Predicates.Evaluation.Support;

namespace Goro.Tests.Predicates.Binding.Support;

/// <summary>Reads predicates from text through the whole compiler, the way every test in this folder wants them.</summary>
internal static class BindAssert
{
    public static CompiledPredicate Compiles(string text, IIdentifierCatalog? catalog = null)
    {
        var result = PredicateCompiler.Compile(text, catalog ?? TestCatalog.Standard());
        Assert.That(result.Succeeded, Is.True, () => $"`{text}` was rejected: {Describe(result.Diagnostics)}");
        return result.Value;
    }

    /// <summary>Every error compiling a predicate reports, in the order reported.</summary>
    public static ImmutableArray<Diagnostic> Errors(string text, IIdentifierCatalog? catalog = null)
    {
        var result = PredicateCompiler.Compile(text, catalog ?? TestCatalog.Standard());
        Assert.That(result.Succeeded, Is.False, $"`{text}` compiled, but should not have");
        return result.Diagnostics;
    }

    /// <summary>The one error compiling a predicate reports.</summary>
    public static Diagnostic Error(string text, IIdentifierCatalog? catalog = null)
    {
        var errors = Errors(text, catalog);
        Assert.That(errors, Has.Length.EqualTo(1), () => $"`{text}`: {Describe(errors)}");
        return errors[0];
    }

    /// <summary>The codes of every error compiling a predicate reports, in order.</summary>
    public static string[] Codes(string text, IIdentifierCatalog? catalog = null) =>
        [.. Errors(text, catalog).Select(d => d.Code)];

    /// <summary>The predicate each suggestion of a diagnostic would turn the text into.</summary>
    public static string[] Rewrites(string text, Diagnostic diagnostic) =>
        [.. diagnostic.Suggestions.Select(s => s.ApplyTo(text))];

    /// <summary>What the text marked by a diagnostic says.</summary>
    public static string Marked(string text, Diagnostic diagnostic) => diagnostic.Span.Of(text);

    /// <summary>Compiles a predicate and evaluates it against one file.</summary>
    public static Outcome Evaluate(string text, IIdentifierCatalog catalog)
    {
        var predicate = Compiles(text, catalog);
        var context = new EvaluationContext(new FileData("any"), predicate.Sources.Count);
        var truth = predicate.Evaluate(context);
        return new Outcome(truth, context.Reported);
    }

    public static string Describe(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(d => $"{d.Code} at {d.Span}: {d.Message}"));
}
