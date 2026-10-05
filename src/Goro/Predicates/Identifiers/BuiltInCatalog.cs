// Owned by the identifier catalog group (G7) of the predicate-runtime-architecture line.
using System.Collections.Immutable;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// Every namespace and identifier docs/features/builtins/identifiers.md defines.
/// </summary>
/// <remarks>
/// The tables below follow that document's, namespace by namespace and row by row, so the two can
/// be checked against each other by eye. Each tag identifier is declared through <see cref="Tag{T}"/>,
/// whose binding is not implemented yet; the line that implements the tag namespaces replaces those
/// rows' bindings, and each open namespace's binding for its other fields, and nothing else here.
/// </remarks>
public sealed class BuiltInCatalog : IIdentifierCatalog
{
    private static readonly ImmutableArray<CatalogNamespace> Namespaces =
    [
        new ClosedNamespace("",
        [
            Tag<string>("artist"),
            Tag<string>("album"),
            Tag<string>("genre"),
            Tag<string>("title"),
            Tag<decimal>("year"),
        ]),
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

    /// <summary>An identifier that reads tags. None is definite, since any tag can be missing.</summary>
    private static DeclarationRow<T> Tag<T>(string name) where T : notnull =>
        new(name, IsDefinite: false, TagBindings.NotImplemented<T>);
}
