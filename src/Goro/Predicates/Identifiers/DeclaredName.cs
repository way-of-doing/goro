namespace Goro.Predicates.Identifiers;

/// <summary>
/// The name of what a declaration reads, independent of how a predicate wrote it: an identifier,
/// or a call of a source function. Two names are equal when they read the same thing, which is what
/// makes them one warning source.
/// </summary>
public abstract class DeclaredName : IEquatable<DeclaredName>
{
    private protected DeclaredName()
    {
    }

    public abstract bool Equals(DeclaredName? other);

    public sealed override bool Equals(object? obj) => Equals(obj as DeclaredName);

    public abstract override int GetHashCode();

    /// <summary>The name as it would be written.</summary>
    public abstract override string ToString();

    private protected static bool SameName(string? x, string? y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
}
