using System.Collections.Immutable;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;
using Goro.Tests.TestSupport;
using Goro.Reading.Bytes;

namespace Goro.Tests.Predicates.Identifiers;

public class BuiltInCatalogTests
{
    private static readonly string[] KnownSources = ["ape", "file", "id3v1", "id3v2", "vorbis"];

    private static readonly string[] TagSources = ["vorbis", "ape", "id3v2", "id3v1"];

    // The table of concepts in docs/features/builtins/identifiers.md, written out independently of the
    // catalog's own so that each checks the other.
    private static readonly (string Concept, GoroType Type)[] Concepts =
    [
        ("artist", GoroType.String),
        ("album", GoroType.String),
        ("genre", GoroType.String),
        ("title", GoroType.String),
        ("track", GoroType.Number),
        ("year", GoroType.Number),
    ];

    private static readonly string[] FileConcepts = ["duration", "extension", "name", "path", "size"];

    private static IEnumerable<TestCaseData> DeclaredIdentifiers()
    {
        TestCaseData Row(string identifier, GoroType type, Bounds bounds) =>
            new TestCaseData(identifier, type, bounds).SetArgDisplayNames(identifier);

        foreach (var (concept, type) in Concepts)
        {
            yield return Row(concept, type, Bounds.Any);
            foreach (var source in TagSources)
            {
                yield return Row($"{source}::{concept}", type, source == "id3v1" ? Bounds.AtMostOne : Bounds.Any);
            }
        }

        yield return Row("file::duration", GoroType.Duration, Bounds.ExactlyOne);
        yield return Row("file::extension", GoroType.String, Bounds.AtMostOne);
        yield return Row("file::name", GoroType.String, Bounds.ExactlyOne);
        yield return Row("file::path", GoroType.String, Bounds.ExactlyOne);
        yield return Row("file::size", GoroType.ByteCount, Bounds.ExactlyOne);
    }

    private static IdentifierName Parse(string written)
    {
        var parts = written.Split("::");
        return parts.Length == 2 ? new IdentifierName(parts[0], parts[1]) : IdentifierName.Plain(written);
    }

    private static IdentifierLookup Lookup(string written) => BuiltInCatalog.Instance.Lookup(Parse(written));

    private static IdentifierDeclaration Found(IdentifierLookup lookup)
    {
        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.Found>());
        return ((IdentifierLookup.Found)lookup).Declaration;
    }

    [TestCaseSource(nameof(DeclaredIdentifiers))]
    public void Lookup_DeclaredIdentifier_IsFoundWithItsTypeAndBounds(string identifier, GoroType type, Bounds bounds)
    {
        var declaration = Found(Lookup(identifier));

        Assert.That(declaration.Name, Is.EqualTo(Parse(identifier)));
        Assert.That(declaration.Type, Is.EqualTo(type));
        Assert.That(declaration.Bounds, Is.EqualTo(bounds));
    }

    [TestCaseSource(nameof(DeclaredIdentifiers))]
    public void Lookup_DeclaredIdentifier_IgnoresCase(string identifier, GoroType type, Bounds bounds)
    {
        var declaration = Found(Lookup(identifier.ToUpperInvariant()));

        Assert.That(declaration.Name, Is.EqualTo(Parse(identifier)));
        Assert.That(declaration.Type, Is.EqualTo(type));
    }

    [Test]
    public void Lookup_DeclaredIdentifier_IsSpelledAsTheTableSpellsIt()
    {
        Assert.That(Found(Lookup("ID3V1::GENRE")).Name.ToString(), Is.EqualTo("id3v1::genre"));
    }

    [TestCase("foo::artist")]
    [TestCase("raw::artist")]
    [TestCase("global::artist")]
    public void Lookup_UnknownSource_ListsTheKnownOnes(string identifier)
    {
        var lookup = Lookup(identifier);

        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.UnknownSource>());
        Assert.That(((IdentifierLookup.UnknownSource)lookup).KnownSources, Is.EqualTo(KnownSources));
    }

    [TestCase("bogus")]
    [TestCase("comment")]
    [TestCase("description")]
    public void Lookup_UnknownConcept_OffersTheConceptsWrittenWithoutASource(string identifier)
    {
        var lookup = Lookup(identifier);

        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.UnknownIdentifier>());
        Assert.That(((IdentifierLookup.UnknownIdentifier)lookup).Candidates, Is.EqualTo(Concepts.Select(row => row.Concept)));
    }

    [TestCase("id3v1::comment", "id3v1")]
    [TestCase("id3v1::size", "id3v1")]
    [TestCase("vorbis::description", "vorbis")]
    [TestCase("vorbis::bpm", "vorbis")]
    [TestCase("id3v2::TIT2", "id3v2")]
    [TestCase("ape::raw", "ape")]
    public void Lookup_ConceptTheSourceCannotSupply_OffersTheSourcesOwn(string identifier, string source)
    {
        var lookup = Lookup(identifier);

        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.UnknownIdentifier>());
        Assert.That(((IdentifierLookup.UnknownIdentifier)lookup).Candidates, Is.EqualTo(Concepts.Select(row => row.Concept)), source);
    }

    [TestCase("file::artist")]
    [TestCase("file::nmae")]
    public void Lookup_UnknownFileConcept_OffersTheFileSourcesOwn(string identifier)
    {
        var lookup = Lookup(identifier);

        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.UnknownIdentifier>());
        Assert.That(((IdentifierLookup.UnknownIdentifier)lookup).Candidates, Is.EqualTo(FileConcepts));
    }

    [TestCaseSource(nameof(FileConcepts))]
    public void Lookup_FileConceptWithoutItsSource_NeedsIt(string concept)
    {
        Assert.That(Lookup(concept), Is.EqualTo(new IdentifierLookup.NeedsSource("file")));
    }

    [TestCase("vorbis::field")]
    [TestCase("ID3V2::BYTES")]
    [TestCase("ape::field")]
    public void Lookup_SourceFunctionWithoutArguments_IsRecognised(string identifier)
    {
        Assert.That(Lookup(identifier), Is.InstanceOf<IdentifierLookup.IsSourceFunction>());
    }

    // ---------------------------------------------------------------------------------------------
    // Source functions

    [TestCase("ape", "field", GoroType.String, 1, 1, false)]
    [TestCase("ape", "bytes", GoroType.Blob, 1, 1, true)]
    [TestCase("id3v2", "field", GoroType.String, 1, 2, false)]
    [TestCase("id3v2", "bytes", GoroType.Blob, 1, 1, false)]
    [TestCase("vorbis", "field", GoroType.String, 1, 1, false)]
    [TestCase("vorbis", "bytes", GoroType.Blob, 1, 1, false)]
    public void LookupFunction_SourceFunction_HasItsTypeArgumentsAndBounds(
        string source, string function, GoroType type, int minimum, int maximum, bool atMostOne)
    {
        var lookup = BuiltInCatalog.Instance.LookupFunction(source.ToUpperInvariant(), function.ToUpperInvariant());

        Assert.That(lookup, Is.InstanceOf<SourceFunctionLookup.Found>());
        var found = ((SourceFunctionLookup.Found)lookup).Function;
        Assert.That(found.Type, Is.EqualTo(type));
        Assert.That((found.MinimumArguments, found.MaximumArguments), Is.EqualTo((minimum, maximum)));

        var arguments = maximum == 1 ? ["TPE1"] : ImmutableArray.Create("TXXX", "MOOD");
        var resolved = found.Resolve([.. arguments]);
        Assert.That(resolved, Is.InstanceOf<SourceFunctionResolution.Found>());
        var declaration = ((SourceFunctionResolution.Found)resolved).Declaration;
        Assert.That(declaration.Type, Is.EqualTo(type));
        Assert.That(declaration.Bounds, Is.EqualTo(atMostOne ? Bounds.AtMostOne : Bounds.Any));
        Assert.That(declaration.Name, Is.EqualTo(new SourceCallName(source, function, [.. arguments])));
    }

    [TestCase("id3v1", "field")]
    [TestCase("file", "field")]
    [TestCase("vorbis", "count")]
    public void LookupFunction_NoSuchFunctionInTheSource_ListsItsOwn(string source, string function)
    {
        var lookup = BuiltInCatalog.Instance.LookupFunction(source, function);

        Assert.That(lookup, Is.InstanceOf<SourceFunctionLookup.UnknownFunction>());
        var expected = source is "id3v1" or "file" ? Array.Empty<string>() : ["field", "bytes"];
        Assert.That(((SourceFunctionLookup.UnknownFunction)lookup).Candidates, Is.EqualTo(expected));
    }

    [Test]
    public void LookupFunction_UnknownSource_ListsTheKnownOnes()
    {
        var lookup = BuiltInCatalog.Instance.LookupFunction("flac", "field");

        Assert.That(lookup, Is.InstanceOf<SourceFunctionLookup.UnknownSource>());
        Assert.That(((SourceFunctionLookup.UnknownSource)lookup).KnownSources, Is.EqualTo(KnownSources));
    }

    [Test]
    public void ResolvingAKeyedSourceFunction_KeepsTheNameAsWritten_AndChecksNothing()
    {
        var field = ((SourceFunctionLookup.Found)BuiltInCatalog.Instance.LookupFunction("ape", "field")).Function;

        var resolved = (SourceFunctionResolution.Found)field.Resolve(["Album Artist"]);

        Assert.That(((SourceCallName)resolved.Declaration.Name).Arguments, Is.EqualTo(new[] { "Album Artist" }));
    }

    [Test]
    public void Resolve_SourceFunction_ReadsTheFilesTags()
    {
        var bytes = ((SourceFunctionLookup.Found)BuiltInCatalog.Instance.LookupFunction("id3v2", "bytes")).Function;
        var declaration = (IdentifierDeclaration<Blob>)((SourceFunctionResolution.Found)bytes.Resolve(["APIC"])).Declaration;
        var path = AudioCorpus.PathOf("mp3/id3v24.mp3");
        using var loader = new FileDataLoader(path, ReadPolicy.Default);

        var value = declaration.Binding.Resolve(new FileData(path, loader), new Origin(new SourceId(0), "x", 0));

        Assert.That(value.Occurrences, Has.Length.EqualTo(1).And.All.InstanceOf<Usable<Blob>>());
    }

    // The expansions identifiers.md gives, written out independently of the catalog's tables.
    [TestCase("artist", "vorbis::artist", "ape::artist", "id3v2::artist", "id3v1::artist")]
    [TestCase("album", "vorbis::album", "ape::album", "id3v2::album", "id3v1::album")]
    [TestCase("genre", "vorbis::genre", "ape::genre", "id3v2::genre", "id3v1::genre")]
    [TestCase("title", "vorbis::title", "ape::title", "id3v2::title", "id3v1::title")]
    [TestCase("track", "vorbis::track", "ape::track", "id3v2::track", "id3v1::track")]
    [TestCase("year", "vorbis::year", "ape::year", "id3v2::year", "id3v1::year")]
    public void ConceptWithoutASource_IsThePreferredOfItsCells_InTheDocumentedOrder(string global, params string[] expansion)
    {
        var declaration = Found(Lookup(global));

        var candidates = declaration switch
        {
            IdentifierDeclaration<string> typed => Candidates(typed),
            IdentifierDeclaration<decimal> typed => Candidates(typed),
            _ => throw new AssertionException($"Unexpected declaration {declaration}"),
        };
        Assert.That(candidates, Has.Length.EqualTo(expansion.Length));
        for (var i = 0; i < expansion.Length; i++)
        {
            var binding = Found(Lookup(expansion[i])) switch
            {
                IdentifierDeclaration<string> typed => (object)typed.Binding,
                IdentifierDeclaration<decimal> typed => typed.Binding,
                var other => throw new AssertionException($"Unexpected declaration {other}"),
            };
            Assert.That(candidates[i], Is.SameAs(binding), $"candidate {i + 1} of {global}");
        }
    }

    private static object[] Candidates<T>(IdentifierDeclaration<T> declaration) where T : notnull
    {
        Assert.That(declaration.Binding, Is.InstanceOf<PreferredBinding<T>>());
        return [.. ((PreferredBinding<T>)declaration.Binding).Candidates];
    }

    private static readonly Occurrence<decimal>[][] Bags =
        [[], [Ok(1991m)], [BadNumber], [Ok(1991m), BadNumber], [BadNumber, BadNumber]];

    // Every combination of two candidates' bags: the binding a global identifier has resolves to what
    // PREFERRED() written over the same identifiers evaluates to, except that its unusable occurrences
    // carry the global identifier's origin.
    [Test]
    public void PreferredBinding_ResolvesAsPreferredEvaluates_WithTheGlobalIdentifiersOrigin()
    {
        foreach (var first in Bags)
        {
            foreach (var second in Bags.Select(bag => bag.Select(o => o is Usable<decimal> ? Ok(2000m) : o).ToArray()))
            {
                var p = new TestPredicate();
                var global = p.Id<decimal>("year");
                var expansion = Preferred(p.Id("vorbis::year", first), p.Id("ape::year", second));
                var binding = new PreferredBinding<decimal>([new CannedBinding<decimal>([.. first]), new CannedBinding<decimal>([.. second])]);

                var resolved = binding.Resolve(p.File, global.Origin);
                var evaluated = p.Evaluate(expansion).Value;

                var context = $"{Value<decimal>.Of(first)} then {Value<decimal>.Of(second)}";
                Assert.That(resolved.IsAbsent, Is.EqualTo(evaluated.IsAbsent), context);
                Assert.That(resolved.Occurrences.Select(Datum), Is.EqualTo(evaluated.Occurrences.Select(Datum)), context);
                Assert.That(resolved.Occurrences.OfType<Unusable<decimal>>().Select(o => o.Origin),
                    Is.All.EqualTo(global.Origin), context);
            }
        }
    }

    private static decimal? Datum(Occurrence<decimal> occurrence) => occurrence is Usable<decimal>(var datum) ? datum : null;

    [Test]
    public void PreferredBinding_DoesNotResolveTheCandidatesAfterTheOneChosen()
    {
        var chosen = new CannedBinding<decimal>([Ok(1991m)]);
        var after = new CannedBinding<decimal>([Ok(2000m)]);
        var binding = new PreferredBinding<decimal>([new CannedBinding<decimal>([BadNumber]), chosen, after]);

        var value = binding.Resolve(TestFiles.Data("any"), new Origin(new SourceId(0), "year", 0));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(1991m) }));
        Assert.That(after.Resolutions, Is.Zero);
    }
}
