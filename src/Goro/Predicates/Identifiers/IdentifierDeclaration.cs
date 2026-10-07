using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// What the binder knows about something a predicate reads from a file -- an identifier, or a call of
/// a source function -- before any file is read: its type, its bounds, and how to resolve it for a
/// file.
/// </summary>
public abstract class IdentifierDeclaration
{
    private protected IdentifierDeclaration(DeclaredName name, Bounds bounds)
    {
        Name = name;
        Bounds = bounds;
    }

    public DeclaredName Name { get; }

    /// <summary>Whether a file evaluated can have none of it, and whether it can have several.</summary>
    public Bounds Bounds { get; }

    public abstract GoroType Type { get; }
}

public sealed class IdentifierDeclaration<T>(DeclaredName name, Bounds bounds, IdentifierBinding<T> binding)
    : IdentifierDeclaration(name, bounds) where T : notnull
{
    public IdentifierBinding<T> Binding { get; } = binding;

    public override GoroType Type => GoroTypes.Of<T>();
}
