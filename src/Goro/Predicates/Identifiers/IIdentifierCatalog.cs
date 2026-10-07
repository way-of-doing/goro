using System.Collections.Immutable;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The sources, concepts and source functions a predicate can name. The binder is given one rather
/// than reaching for the built-in catalog, which is what lets a test declare identifiers holding
/// whatever it needs.
/// </summary>
public interface IIdentifierCatalog
{
    /// <summary>The concept an identifier names, in its source if it has one.</summary>
    IdentifierLookup Lookup(IdentifierName name);

    /// <summary>The source function a qualified call names.</summary>
    SourceFunctionLookup LookupFunction(string source, string function);
}

public abstract record IdentifierLookup
{
    private IdentifierLookup()
    {
    }

    public sealed record Found(IdentifierDeclaration Declaration) : IdentifierLookup;

    /// <summary>The source is not one Goro defines; <paramref name="KnownSources"/> are the ones it does.</summary>
    public sealed record UnknownSource(ImmutableArray<string> KnownSources) : IdentifierLookup;

    /// <summary>
    /// No concept of that name, or none the source can supply; <paramref name="Candidates"/> are the
    /// concepts there are where it was looked for, for suggesting the closest.
    /// </summary>
    public sealed record UnknownIdentifier(ImmutableArray<string> Candidates) : IdentifierLookup;

    /// <summary>A concept that is always written with its source, written without one.</summary>
    public sealed record NeedsSource(string Source) : IdentifierLookup;

    /// <summary>A source function of the source, written without the arguments that make it a call.</summary>
    public sealed record IsSourceFunction : IdentifierLookup;
}

public abstract record SourceFunctionLookup
{
    private SourceFunctionLookup()
    {
    }

    public sealed record Found(SourceFunction Function) : SourceFunctionLookup;

    /// <summary>The source is not one Goro defines; <paramref name="KnownSources"/> are the ones it does.</summary>
    public sealed record UnknownSource(ImmutableArray<string> KnownSources) : SourceFunctionLookup;

    /// <summary>The source has no function of that name; <paramref name="Candidates"/> are the ones it has.</summary>
    public sealed record UnknownFunction(ImmutableArray<string> Candidates) : SourceFunctionLookup;
}
