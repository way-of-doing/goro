using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// What the binder knows about an identifier before any file is read: its type, its bounds, and how
/// to resolve it for a file.
/// </summary>
public abstract class IdentifierDeclaration
{
    private protected IdentifierDeclaration(IdentifierName name, Bounds bounds)
    {
        Name = name;
        Bounds = bounds;
    }

    public IdentifierName Name { get; }

    /// <summary>Whether a file evaluated can have none of it, and whether it can have several.</summary>
    public Bounds Bounds { get; }

    public abstract GoroType Type { get; }
}

public sealed class IdentifierDeclaration<T>(IdentifierName name, Bounds bounds, IdentifierBinding<T> binding)
    : IdentifierDeclaration(name, bounds) where T : notnull
{
    public IdentifierBinding<T> Binding { get; } = binding;

    public override GoroType Type => GoroTypes.Of<T>();
}
