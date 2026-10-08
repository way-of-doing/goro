// Owned by the identifier catalog group (G7) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// Every source, concept and source function docs/features/builtins/identifiers.md defines.
/// </summary>
/// <remarks>
/// <see cref="Table"/> follows that document's table of concepts, row by row and cell by cell, so
/// the two can be checked against each other by eye. The source functions are bound to the file's
/// tags through <see cref="TagBindings"/>; every cell is still bound to a
/// <see cref="TagBindings.NotImplemented{T}"/> binding, which step 2c of
/// docs/directions/mp3-support.md replaces. A concept written without a source is a
/// <see cref="PreferredBinding{T}"/> over its cells, in <see cref="TagSources"/> order.
/// </remarks>
public sealed class BuiltInCatalog : IIdentifierCatalog
{
    private const string Ape = "ape";
    private const string Id3v1 = "id3v1";
    private const string Id3v2 = "id3v2";
    private const string Vorbis = "vorbis";

    /// <summary>The tag sources, most preferred first, which is also the order of each row's cells.</summary>
    private static readonly string[] TagSources = [Vorbis, Ape, Id3v2, Id3v1];

    /// <summary>The concepts of the tag sources, and the field each source's cell reads.</summary>
    private static readonly ImmutableArray<ConceptRow> Table =
    [
        new ConceptRow<string>("artist", ["ARTIST", "Artist", "TPE1", "artist"]),
        new ConceptRow<string>("album", ["ALBUM", "Album", "TALB", "album"]),
        new ConceptRow<string>("genre", ["GENRE", "Genre", "TCON", "genre"]),
        new ConceptRow<string>("title", ["TITLE", "Title", "TIT2", "title"]),
        new ConceptRow<decimal>("track", ["TRACKNUMBER", "Track", "TRCK", "track"]),
        new ConceptRow<decimal>("year", ["DATE", "Year", "TDRC", "year"]),
    ];

    private static readonly ImmutableArray<CatalogSource> Sources =
    [
        new(Ape, Cells(Ape),
        [
            Field(Ape, 1),
            Bytes(Ape, Bounds.AtMostOne),
        ]),
        new(FileSource.Name, FileSource.Concepts, []),
        new(Id3v1, Cells(Id3v1), []),
        new(Id3v2, Cells(Id3v2),
        [
            Field(Id3v2, 2, Id3v2Frames.CheckField),
            Bytes(Id3v2, Bounds.Any, Id3v2Frames.CheckBytes),
        ]),
        new(Vorbis, Cells(Vorbis),
        [
            Field(Vorbis, 1),
            Bytes(Vorbis, Bounds.Any),
        ]),
    ];

    private static readonly ImmutableArray<string> KnownSources = [.. Sources.Select(source => source.Name)];

    private static readonly Dictionary<string, IdentifierDeclaration> Plain =
        Table.Select(row => row.Plain(TagSources.Select(source => Cell(source, row.Name)))).ToDictionary(Concept, StringComparer.OrdinalIgnoreCase);

    public static BuiltInCatalog Instance { get; } = new();

    private BuiltInCatalog()
    {
    }

    public IdentifierLookup Lookup(IdentifierName name)
    {
        if (name.Source is null)
        {
            if (Plain.TryGetValue(name.Name, out var plain))
            {
                return new IdentifierLookup.Found(plain);
            }

            return SourceOf(FileSource.Name).Concept(name.Name) is not null
                ? new IdentifierLookup.NeedsSource(FileSource.Name)
                : new IdentifierLookup.UnknownIdentifier([.. Plain.Values.Select(Concept)]);
        }

        if (Find(name.Source) is not { } source)
        {
            return new IdentifierLookup.UnknownSource(KnownSources);
        }

        if (source.Concept(name.Name) is { } declaration)
        {
            return new IdentifierLookup.Found(declaration);
        }

        return source.Function(name.Name) is not null
            ? new IdentifierLookup.IsSourceFunction()
            : new IdentifierLookup.UnknownIdentifier(source.ConceptNames);
    }

    public SourceFunctionLookup LookupFunction(string source, string function)
    {
        if (Find(source) is not { } found)
        {
            return new SourceFunctionLookup.UnknownSource(KnownSources);
        }

        return found.Function(function) is { } named
            ? new SourceFunctionLookup.Found(named)
            : new SourceFunctionLookup.UnknownFunction(found.FunctionNames);
    }

    private static CatalogSource? Find(string name) =>
        Sources.FirstOrDefault(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase));

    private static CatalogSource SourceOf(string name) => Find(name)!;

    private static IdentifierDeclaration Cell(string source, string concept) =>
        SourceOf(source).Concept(concept)!;

    private static string Concept(IdentifierDeclaration declaration) => ((IdentifierName)declaration.Name).Name;

    /// <summary>The cells a tag source has: one for every concept of the table, Id3v1's never holding more than one occurrence.</summary>
    private static ImmutableArray<IdentifierDeclaration> Cells(string source)
    {
        var bounds = source == Id3v1 ? Bounds.AtMostOne : Bounds.Any;
        return [.. Table.Select(row => row.Cell(source, bounds))];
    }

    /// <summary><c>field()</c>: the data as text, as recorded, one occurrence for each value the format records.</summary>
    private static SourceFunction Field(string source, int maximumArguments, Func<ImmutableArray<string>, ArgumentCheck>? check = null) =>
        new SourceFunction<string>(source, "field", 1, maximumArguments, Bounds.Any, TagBindings.Field(source), check);

    /// <summary><c>bytes()</c>: the data as recorded, one occurrence for each item, frame or comment.</summary>
    private static SourceFunction Bytes(string source, Bounds bounds, Func<ImmutableArray<string>, ArgumentCheck>? check = null) =>
        new SourceFunction<Blob>(source, "bytes", 1, 1, bounds, TagBindings.Bytes(source), check);

    /// <summary>A row of the table of concepts: a concept's name, its type, and the field each tag source's cell reads.</summary>
    private abstract record ConceptRow(string Name, ImmutableArray<string> Fields)
    {
        public abstract IdentifierDeclaration Cell(string source, Bounds bounds);

        /// <summary>The concept written without a source: the preference among its cells.</summary>
        public abstract IdentifierDeclaration Plain(IEnumerable<IdentifierDeclaration> cells);
    }

    private sealed record ConceptRow<T>(string Name, ImmutableArray<string> Fields) : ConceptRow(Name, Fields) where T : notnull
    {
        public override IdentifierDeclaration Cell(string source, Bounds bounds)
        {
            var name = new IdentifierName(source, Name);
            return new IdentifierDeclaration<T>(name, bounds, TagBindings.NotImplemented<T>(name));
        }

        public override IdentifierDeclaration Plain(IEnumerable<IdentifierDeclaration> cells)
        {
            var candidates = cells.Cast<IdentifierDeclaration<T>>().ToList();
            return new IdentifierDeclaration<T>(
                IdentifierName.Plain(Name),
                Bounds.Preferred(candidates.Select(candidate => candidate.Bounds)),
                new PreferredBinding<T>([.. candidates.Select(candidate => candidate.Binding)]));
        }
    }

    /// <summary>A source: the concepts it supplies and the source functions it has.</summary>
    private sealed class CatalogSource(string name, ImmutableArray<IdentifierDeclaration> concepts, ImmutableArray<SourceFunction> functions)
    {
        public string Name { get; } = name;

        public ImmutableArray<string> ConceptNames { get; } = [.. concepts.Select(BuiltInCatalog.Concept)];

        public ImmutableArray<string> FunctionNames { get; } = [.. functions.Select(function => function.Name)];

        public IdentifierDeclaration? Concept(string concept) =>
            concepts.FirstOrDefault(declaration => string.Equals(BuiltInCatalog.Concept(declaration), concept, StringComparison.OrdinalIgnoreCase));

        public SourceFunction? Function(string function) =>
            functions.FirstOrDefault(candidate => string.Equals(candidate.Name, function, StringComparison.OrdinalIgnoreCase));
    }
}
