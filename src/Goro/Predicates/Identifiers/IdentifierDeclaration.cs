using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// What the binder knows about an identifier before any file is read: its type, whether it is
/// definite, and how to resolve it for a file.
/// </summary>
public abstract class IdentifierDeclaration
{
    private protected IdentifierDeclaration(IdentifierName name, bool isDefinite)
    {
        Name = name;
        IsDefinite = isDefinite;
    }

    public IdentifierName Name { get; }

    /// <summary>Whether every file evaluated has exactly one occurrence of it.</summary>
    public bool IsDefinite { get; }

    public abstract GoroType Type { get; }

    /// <summary>
    /// A reference to this identifier. The declaration knows its own datum type, so this is how
    /// lowering gets a typed node from a name the binder looked up.
    /// </summary>
    public abstract Expression Bind(Origin origin);
}

public sealed class IdentifierDeclaration<T>(IdentifierName name, bool isDefinite, IdentifierBinding<T> binding)
    : IdentifierDeclaration(name, isDefinite) where T : notnull
{
    public IdentifierBinding<T> Binding { get; } = binding;

    public override GoroType Type => GoroTypes.Of<T>();

    public override Expression Bind(Origin origin) => new IdentifierReference<T>(this, origin);
}
