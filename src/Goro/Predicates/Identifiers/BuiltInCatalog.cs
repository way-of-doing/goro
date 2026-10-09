// Owned by the identifier catalog group (G7) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using Goro.Predicates.Identifiers.Interpretation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// Every source, concept and source function docs/features/builtins/identifiers.md defines.
/// </summary>
/// <remarks>
/// <see cref="Table"/> follows that document's table of concepts, row by row and cell by cell, so
/// the two can be checked against each other by eye. Each row also says how its concept interprets
/// what it reads. The source functions and the cells are bound to the file's tags through
/// <see cref="TagBindings"/>. A concept written without a source is a
/// <see cref="PreferredBinding{T}"/> over its cells, in <see cref="TagSources"/> order.
/// </remarks>
public sealed class BuiltInCatalog : IIdentifierCatalog
{
    private const string Ape = SourceNames.Ape;
    private const string Id3v1 = SourceNames.Id3v1;
    private const string Id3v2 = SourceNames.Id3v2;
    private const string Vorbis = SourceNames.Vorbis;

    /// <summary>The tag sources, most preferred first, which is also the order of each row's cells.</summary>
    private static readonly string[] TagSources = [Vorbis, Ape, Id3v2, Id3v1];

    /// <summary>
    /// The concepts of the tag sources, the field each source's cell reads, and how the concept
    /// interprets it. The fields of a row are in <see cref="TagSources"/> order.
    /// </summary>
    private static readonly ImmutableArray<ConceptRow> Table =
    [
        new ConceptRow<string>("artist",
            [ConceptFieldMapping.Vorbis.Artist, ConceptFieldMapping.Ape.Artist, ConceptFieldMapping.Id3v2.Artist, ConceptFieldMapping.Id3v1.Artist],
            ConceptInterpretations.Strings),
        new ConceptRow<string>("album",
            [ConceptFieldMapping.Vorbis.Album, ConceptFieldMapping.Ape.Album, ConceptFieldMapping.Id3v2.Album, ConceptFieldMapping.Id3v1.Album],
            ConceptInterpretations.Strings),
        new ConceptRow<string>("genre",
            [ConceptFieldMapping.Vorbis.Genre, ConceptFieldMapping.Ape.Genre, ConceptFieldMapping.Id3v2.Genre, ConceptFieldMapping.Id3v1.Genre],
            ConceptInterpretations.Genres),
        new ConceptRow<string>("title",
            [ConceptFieldMapping.Vorbis.Title, ConceptFieldMapping.Ape.Title, ConceptFieldMapping.Id3v2.Title, ConceptFieldMapping.Id3v1.Title],
            ConceptInterpretations.Strings),
        new ConceptRow<decimal>("track",
            [ConceptFieldMapping.Vorbis.Track, ConceptFieldMapping.Ape.Track, ConceptFieldMapping.Id3v2.Track, ConceptFieldMapping.Id3v1.Track],
            ConceptInterpretations.Tracks),
        new ConceptRow<decimal>("year",
            [ConceptFieldMapping.Vorbis.Year, ConceptFieldMapping.Ape.Year, ConceptFieldMapping.Id3v2.Year, ConceptFieldMapping.Id3v1.Year],
            ConceptInterpretations.Years),
    ];

    private static readonly ImmutableArray<CatalogSource> Sources =
    [
        new(Ape, Cells(Ape),
        [
            Field(Ape, 1),
            Bytes(Ape, Bounds.AtMostOne),
        ]),
        new(SourceNames.File, FileSource.Concepts, []),
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

            return SourceOf(SourceNames.File).Concept(name.Name) is not null
                ? new IdentifierLookup.NeedsSource(SourceNames.File)
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

    /// <summary>A row of the table of concepts: a concept's name, its type, and the field each tag source's cell reads, in <see cref="TagSources"/> order.</summary>
    private abstract record ConceptRow(string Name, ImmutableArray<string> Fields)
    {
        public abstract IdentifierDeclaration Cell(string source, Bounds bounds);

        /// <summary>The concept written without a source: the preference among its cells.</summary>
        public abstract IdentifierDeclaration Plain(IEnumerable<IdentifierDeclaration> cells);
    }

    private sealed record ConceptRow<T>(string Name, ImmutableArray<string> Fields, ConceptInterpretation<T> Interpretation)
        : ConceptRow(Name, Fields) where T : notnull
    {
        public override IdentifierDeclaration Cell(string source, Bounds bounds)
        {
            var field = Fields[Array.IndexOf(TagSources, source)];
            return new IdentifierDeclaration<T>(new IdentifierName(source, Name), bounds, TagBindings.Cell(source, field, Interpretation));
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
