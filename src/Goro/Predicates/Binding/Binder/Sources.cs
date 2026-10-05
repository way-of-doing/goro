// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// Interns warning sources: every structurally distinct sub-expression that can give birth to an
/// unusable occurrence -- an identifier reference or a conversion -- gets the next
/// <see cref="SourceId"/>, and every node of the same shape shares it.
/// </summary>
/// <remarks>
/// Children are bound, and so interned, before their parents, which is what lets a parent's shape
/// be its symbol plus its children's shapes. Identifiers are numbered by <see cref="IdentifierName"/>
/// equality, so the spellings that name one identifier -- in any case, quoted or bare, with or
/// without a leading <c>::</c> -- can never make two sources.
/// </remarks>
internal sealed class Sources(string text)
{
    private readonly Dictionary<IdentifierName, int> identifiers = [];
    private readonly Dictionary<string, SourceId> ids = new(StringComparer.Ordinal);
    private readonly List<string> forms = [];

    public Shape Identifier(IdentifierName name)
    {
        if (!identifiers.TryGetValue(name, out var number))
        {
            number = identifiers.Count;
            identifiers.Add(name, number);
        }

        var lowered = new IdentifierName(name.Namespace.Select(Lower), Lower(name.Name));
        return new Shape($"#{number}", lowered.ToString());
    }

    /// <summary>The origin of a node of this shape, written at <paramref name="span"/>.</summary>
    public Origin Origin(Shape shape, TextSpan span)
    {
        if (!ids.TryGetValue(shape.Key, out var id))
        {
            id = new SourceId(forms.Count);
            ids.Add(shape.Key, id);
            forms.Add(shape.Form);
        }

        return new Origin(id, span.Of(text));
    }

    public SourceTable Table() => new([.. forms]);

    private static string Lower(string part) => part.ToLowerInvariant();
}
