using Goro.Messages;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// What the makers of static analysis's errors share: quoting what the user wrote. The binder and
/// each analysis have their own, holding the errors for the rules they own and the rewrites offered
/// for them, every rewrite composed from the spans of what the user wrote, so that it changes only
/// what was wrong.
/// </summary>
internal abstract class SemanticDiagnostics(string text)
{
    protected string Text(SyntaxNode node) => Text(node.Span);

    protected string Text(TextSpan span) => span.Of(text);

    protected Code Code(SyntaxNode node) => new(Text(node));

    protected Code Code(TextSpan span) => new(Text(span));

    protected static Code Keyword(ModifierSyntax modifier) => new(modifier.Modifier.ToString().ToUpperInvariant());

    /// <summary>The two ends of a range and what lies between them: <c>"a".."b"</c>.</summary>
    protected static TextSpan RangeSpan(BetweenSyntax between) => TextSpan.Covering(between.Minimum.Span, between.Maximum.Span);

    /// <summary>Bytecount or duration: the only types whose literals carry a unit.</summary>
    protected static UnitType UnitOf(GoroType type) => type switch
    {
        GoroType.ByteCount => UnitType.ByteCount,
        GoroType.Duration => UnitType.Duration,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Only a bytecount or a duration has a unit."),
    };
}
