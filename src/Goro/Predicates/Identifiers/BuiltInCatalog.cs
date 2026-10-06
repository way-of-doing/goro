// Owned by the identifier catalog group (G7) of the predicate-runtime-architecture line.
using System.Collections.Immutable;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// Every namespace and identifier docs/features/builtins/identifiers.md defines.
/// </summary>
/// <remarks>
/// The tables below follow that document's, namespace by namespace and row by row, so the two can
/// be checked against each other by eye; the global namespace comes last only because it is built
/// from the others. Each tag identifier of a format is declared through <see cref="Tag{T}"/>, whose
/// binding is not implemented yet; the line that implements the tag namespaces replaces those rows'
/// bindings, and each open namespace's binding for its other fields, and nothing else here. Each
/// global identifier is a <see cref="PreferredBinding{T}"/> over the format identifiers of its name,
/// and follows them.
/// </remarks>
public sealed class BuiltInCatalog : IIdentifierCatalog
{
    private static readonly ImmutableArray<CatalogNamespace> Formats =
    [
        new OpenNamespace("ape", TagBindings.NotImplemented<string>,
        [
            Tag<string>("artist"),
            Tag<string>("album"),
            Tag<string>("comment"),
            Tag<string>("genre"),
            Tag<string>("title"),
            Tag<decimal>("track"),
            Tag<decimal>("year"),
        ]),
        new OpenNamespace("ape::raw", TagBindings.NotImplemented<string>, []),
        new ClosedNamespace("file", FileNamespace.Identifiers),
        new ClosedNamespace("id3v1",
        [
            Tag<string>("artist"),
            Tag<string>("album"),
            Tag<string>("comment"),
            Tag<string>("genre"),
            Tag<string>("title"),
            Tag<decimal>("track"),
            Tag<decimal>("year"),
        ]),
        new ClosedNamespace("id3v1::raw",
        [
            Tag<string>("artist"),
            Tag<string>("album"),
            Tag<string>("comment"),
            Tag<decimal>("genre"),
            Tag<string>("title"),
            Tag<decimal>("track"),
            Tag<string>("year"),
        ]),
        new OpenNamespace("id3v2", TagBindings.NotImplemented<string>,
        [
            Tag<string>("artist"),
            Tag<string>("album"),
            Tag<string>("comment"),
            Tag<string>("genre"),
            Tag<string>("title"),
            Tag<decimal>("track"),
            Tag<decimal>("year"),
        ]),
        new OpenNamespace("id3v2::raw", TagBindings.NotImplemented<string>, []),
        new OpenNamespace("vorbis", TagBindings.NotImplemented<string>,
        [
            Tag<string>("artist"),
            Tag<string>("album"),
            Tag<string>("description"),
            Tag<string>("genre"),
            Tag<string>("title"),
            Tag<decimal>("track"),
            Tag<decimal>("year"),
        ]),
        new OpenNamespace("vorbis::raw", TagBindings.NotImplemented<string>, []),
    ];

    /// <summary>The formats a global identifier draws on, most preferred first.</summary>
    private static readonly string[] PreferenceOrder = ["vorbis", "ape", "id3v2", "id3v1"];

    private static readonly ImmutableArray<CatalogNamespace> Namespaces =
    [
        new ClosedNamespace("",
        [
            Preferred<string>("artist"),
            Preferred<string>("album"),
            Preferred<string>("genre"),
            Preferred<string>("title"),
            Preferred<decimal>("year"),
        ]),
        .. Formats,
    ];

    // The global namespace is left out: it is never the one a predicate failed to name.
    private static readonly ImmutableArray<string> KnownNamespaces =
        [.. Namespaces.Where(@namespace => !@namespace.IsGlobal).Select(@namespace => @namespace.Spelling)];

    public static BuiltInCatalog Instance { get; } = new();

    private BuiltInCatalog()
    {
    }

    /// <remarks>
    /// <c>id3v2::raw</c> written as an identifier is, by the grammar, the name <c>raw</c> in the open
    /// namespace <c>id3v2</c>, and so reads a field named "raw" like any other name there. The same
    /// goes for <c>ape::raw</c> and <c>vorbis::raw</c>; in the closed <c>id3v1</c> it is unknown.
    /// </remarks>
    public IdentifierLookup Lookup(IdentifierName name) =>
        Namespaces.FirstOrDefault(@namespace => @namespace.Contains(name)) is { } found
            ? found.Lookup(name)
            : new IdentifierLookup.UnknownNamespace(KnownNamespaces);

    /// <summary>
    /// A global identifier: the <c>PREFERRED()</c> of the identifiers of the same name in the formats,
    /// in order of preference. None is definite, since every format's can be missing.
    /// </summary>
    private static DeclarationRow<T> Preferred<T>(string name) where T : notnull
    {
        var binding = new PreferredBinding<T>([.. PreferenceOrder.Select(format => FormatBinding<T>(format, name))]);
        return new(name, IsDefinite: false, _ => binding);
    }

    private static IdentifierBinding<T> FormatBinding<T>(string format, string name) where T : notnull =>
        Formats.Single(@namespace => @namespace.Spelling == format).Declarations
            .OfType<IdentifierDeclaration<T>>()
            .Single(declaration => declaration.Name.Name == name)
            .Binding;

    /// <summary>An identifier that reads tags. None is definite, since any tag can be missing.</summary>
    private static DeclarationRow<T> Tag<T>(string name) where T : notnull =>
        new(name, IsDefinite: false, TagBindings.NotImplemented<T>);
}
