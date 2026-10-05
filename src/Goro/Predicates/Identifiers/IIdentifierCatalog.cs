using System.Collections.Immutable;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The identifiers a predicate can name. The binder is given one rather than reaching for the
/// built-in catalog, which is what lets a test declare identifiers holding whatever it needs.
/// </summary>
public interface IIdentifierCatalog
{
    IdentifierLookup Lookup(IdentifierName name);
}

public abstract record IdentifierLookup
{
    private IdentifierLookup()
    {
    }

    public sealed record Found(IdentifierDeclaration Declaration) : IdentifierLookup;

    /// <summary>The namespace is not one Goro defines; <paramref name="KnownNamespaces"/> are the ones it does.</summary>
    public sealed record UnknownNamespace(ImmutableArray<string> KnownNamespaces) : IdentifierLookup;

    /// <summary>
    /// The namespace is closed and defines no such identifier; <paramref name="Candidates"/> are the
    /// ones it does define, for suggesting the closest.
    /// </summary>
    public sealed record UnknownIdentifier(ImmutableArray<IdentifierName> Candidates) : IdentifierLookup;
}
