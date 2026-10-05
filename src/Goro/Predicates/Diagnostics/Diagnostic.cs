using System.Collections.Immutable;
using Goro.Messages;
using Goro.Predicates.Syntax;

namespace Goro.Predicates.Diagnostics;

/// <summary>
/// An error found while reading a predicate. <paramref name="Code"/> is a short kebab-case name
/// such as <c>unknown-escape</c>, stable enough to test against; each stage defines its own.
/// <paramref name="Message"/> is the error as data, which an <see cref="IErrorMessages"/> puts into words.
/// </summary>
public sealed record Diagnostic(string Code, TextSpan Span, ErrorMessage Message, ImmutableArray<Suggestion> Suggestions)
{
    public Diagnostic(string code, TextSpan span, ErrorMessage message)
        : this(code, span, message, [])
    {
    }
}

/// <summary>
/// One alternative way to write the predicate: <paramref name="Span"/> of the original text is to be
/// replaced by <paramref name="Replacement"/>. A replacement is composed from the user's own text
/// wherever it can be.
/// </summary>
public sealed record Suggestion(TextSpan Span, string Replacement)
{
    public string ApplyTo(string text) => string.Concat(text.AsSpan(0, Span.Start), Replacement, text.AsSpan(Span.End));
}
