using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Identifiers;

public class BuiltInCatalogTests
{
    private static readonly string[] KnownNamespaces =
        ["ape", "ape::raw", "file", "id3v1", "id3v1::raw", "id3v2", "id3v2::raw", "vorbis", "vorbis::raw"];

    // Every identifier the tables of docs/features/builtins/identifiers.md list, written out
    // independently of the catalog's own tables so that each checks the other.
    private static readonly (string Namespace, string[] Names)[] ClosedNamespaces =
    [
        ("", ["artist", "album", "genre", "title", "year"]),
        ("file", ["duration", "extension", "name", "path", "size"]),
        ("id3v1", ["artist", "album", "comment", "genre", "title", "track", "year"]),
        ("id3v1::raw", ["artist", "album", "comment", "genre", "title", "track", "year"]),
    ];

    private static IEnumerable<TestCaseData> DeclaredIdentifiers()
    {
        TestCaseData Row(string identifier, GoroType type, bool isDefinite = false) =>
            new TestCaseData(identifier, type, isDefinite).SetArgDisplayNames(identifier);

        yield return Row("artist", GoroType.String);
        yield return Row("album", GoroType.String);
        yield return Row("genre", GoroType.String);
        yield return Row("title", GoroType.String);
        yield return Row("year", GoroType.Number);

        yield return Row("file::duration", GoroType.Duration);
        yield return Row("file::extension", GoroType.String);
        yield return Row("file::name", GoroType.String, isDefinite: true);
        yield return Row("file::path", GoroType.String, isDefinite: true);
        yield return Row("file::size", GoroType.ByteCount, isDefinite: true);

        foreach (var @namespace in new[] { "id3v1", "id3v1::raw" })
        {
            yield return Row($"{@namespace}::artist", GoroType.String);
            yield return Row($"{@namespace}::album", GoroType.String);
            yield return Row($"{@namespace}::comment", GoroType.String);
            yield return Row($"{@namespace}::title", GoroType.String);
            yield return Row($"{@namespace}::track", GoroType.Number);
        }

        yield return Row("id3v1::genre", GoroType.String);
        yield return Row("id3v1::year", GoroType.Number);
        yield return Row("id3v1::raw::genre", GoroType.Number);
        yield return Row("id3v1::raw::year", GoroType.String);

        foreach (var @namespace in new[] { "ape", "id3v2", "vorbis" })
        {
            yield return Row($"{@namespace}::artist", GoroType.String);
            yield return Row($"{@namespace}::album", GoroType.String);
            yield return Row($"{@namespace}::genre", GoroType.String);
            yield return Row($"{@namespace}::title", GoroType.String);
            yield return Row($"{@namespace}::track", GoroType.Number);
            yield return Row($"{@namespace}::year", GoroType.Number);
        }

        yield return Row("ape::comment", GoroType.String);
        yield return Row("id3v2::comment", GoroType.String);
        yield return Row("vorbis::description", GoroType.String);
    }

    private static IdentifierName Parse(string written)
    {
        var parts = written.Split("::");
        return new IdentifierName(parts[..^1], parts[^1]);
    }

    private static IdentifierLookup Lookup(string written) => BuiltInCatalog.Instance.Lookup(Parse(written));

    private static IdentifierDeclaration Found(IdentifierLookup lookup)
    {
        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.Found>());
        return ((IdentifierLookup.Found)lookup).Declaration;
    }

    [TestCaseSource(nameof(DeclaredIdentifiers))]
    public void Lookup_DeclaredIdentifier_IsFoundWithItsTypeAndDefiniteness(string identifier, GoroType type, bool isDefinite)
    {
        var declaration = Found(Lookup(identifier));

        Assert.That(declaration.Name, Is.EqualTo(Parse(identifier)));
        Assert.That(declaration.Type, Is.EqualTo(type));
        Assert.That(declaration.IsDefinite, Is.EqualTo(isDefinite));
    }

    [TestCaseSource(nameof(DeclaredIdentifiers))]
    public void Lookup_DeclaredIdentifier_IgnoresCase(string identifier, GoroType type, bool isDefinite)
    {
        var declaration = Found(Lookup(identifier.ToUpperInvariant()));

        Assert.That(declaration.Name, Is.EqualTo(Parse(identifier)));
        Assert.That(declaration.Type, Is.EqualTo(type));
    }

    [Test]
    public void Lookup_DeclaredIdentifier_IsSpelledAsTheTableSpellsIt()
    {
        var declaration = Found(Lookup("ID3V1::RAW::GENRE"));

        Assert.That(declaration.Name.ToString(), Is.EqualTo("id3v1::raw::genre"));
    }

    [TestCase("foo::artist")]
    [TestCase("raw::artist")]
    [TestCase("file::raw::path")]
    [TestCase("id3v1::raw::raw::artist")]
    [TestCase("id3v2::raw::TIT2::x")]
    [TestCase("vorbis::raw::raw::artist")]
    public void Lookup_UnknownNamespace_ListsTheKnownOnes(string identifier)
    {
        var lookup = Lookup(identifier);

        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.UnknownNamespace>());
        Assert.That(((IdentifierLookup.UnknownNamespace)lookup).KnownNamespaces, Is.EquivalentTo(KnownNamespaces));
    }

    [Test]
    public void Lookup_QuotedNamespacePartContainingColons_IsNotTheNestedNamespace()
    {
        var lookup = BuiltInCatalog.Instance.Lookup(new IdentifierName(["ape::raw"], "artist"));

        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.UnknownNamespace>());
    }

    [TestCase("bogus", "")]
    [TestCase("and", "")]
    [TestCase("file::nmae", "file")]
    [TestCase("file::raw", "file")]
    [TestCase("id3v1::TIT2", "id3v1")]
    [TestCase("id3v1::raw", "id3v1")]
    [TestCase("id3v1::raw::raw", "id3v1::raw")]
    [TestCase("id3v1::raw::date", "id3v1::raw")]
    public void Lookup_UnknownNameInClosedNamespace_OffersThatNamespacesIdentifiers(string identifier, string @namespace)
    {
        var expected = ClosedNamespaces.Single(entry => entry.Namespace == @namespace).Names
            .Select(name => @namespace.Length == 0 ? name : $"{@namespace}::{name}");

        var lookup = Lookup(identifier);

        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.UnknownIdentifier>());
        var candidates = ((IdentifierLookup.UnknownIdentifier)lookup).Candidates.Select(candidate => candidate.ToString());
        Assert.That(candidates, Is.EqualTo(expected));
    }

    [TestCase(new[] { "ape" }, "Album Artist")]
    [TestCase(new[] { "ape" }, "raw")]
    [TestCase(new[] { "ape", "raw" }, "track")]
    [TestCase(new[] { "ape", "raw" }, "Cover Art (Front)")]
    [TestCase(new[] { "id3v2" }, "TRCK")]
    [TestCase(new[] { "id3v2" }, "TIT3")]
    [TestCase(new[] { "id3v2" }, "raw")]
    [TestCase(new[] { "id3v2", "raw" }, "TCON")]
    [TestCase(new[] { "vorbis" }, "date")]
    [TestCase(new[] { "vorbis" }, "raw")]
    [TestCase(new[] { "vorbis", "raw" }, "year")]
    [TestCase(new[] { "vorbis", "raw" }, "tracknumber")]
    public void Lookup_AnyOtherNameInOpenNamespace_IsAStringThatIsNotDefinite(string[] @namespace, string name)
    {
        var identifier = new IdentifierName(@namespace, name);

        var declaration = Found(BuiltInCatalog.Instance.Lookup(identifier));

        Assert.That(declaration.Name, Is.EqualTo(identifier));
        Assert.That(declaration.Type, Is.EqualTo(GoroType.String));
        Assert.That(declaration.IsDefinite, Is.False);
    }

    [Test]
    public void Lookup_WellKnownNameInOpenNamespace_TakesPrecedenceOverTheFieldOfThatName()
    {
        Assert.That(Found(Lookup("vorbis::year")).Type, Is.EqualTo(GoroType.Number));
        Assert.That(Found(Lookup("vorbis::raw::year")).Type, Is.EqualTo(GoroType.String));
    }

    [TestCase("artist")]
    [TestCase("id3v1::raw::genre")]
    [TestCase("ape::year")]
    [TestCase("id3v2::TIT3")]
    [TestCase("vorbis::raw::custom")]
    public void Resolve_TagIdentifier_IsNotImplementedYet(string identifier)
    {
        var declaration = Found(Lookup(identifier));
        var file = new FileData("/music/track.mp3");
        var origin = new Origin(new SourceId(0), identifier, 0);

        TestDelegate resolve = declaration switch
        {
            IdentifierDeclaration<string> typed => () => typed.Binding.Resolve(file, origin),
            IdentifierDeclaration<decimal> typed => () => typed.Binding.Resolve(file, origin),
            _ => throw new AssertionException($"Unexpected declaration {declaration}"),
        };

        Assert.That(resolve, Throws.TypeOf<NotSupportedException>().With.Message.Contains("not implemented yet"));
    }

    // The expansions identifiers.md gives, written out independently of the catalog's tables.
    [TestCase("artist", "vorbis::artist", "ape::artist", "id3v2::artist", "id3v1::artist")]
    [TestCase("album", "vorbis::album", "ape::album", "id3v2::album", "id3v1::album")]
    [TestCase("genre", "vorbis::genre", "ape::genre", "id3v2::genre", "id3v1::genre")]
    [TestCase("title", "vorbis::title", "ape::title", "id3v2::title", "id3v1::title")]
    [TestCase("year", "vorbis::year", "ape::year", "id3v2::year", "id3v1::year")]
    public void GlobalIdentifier_IsThePreferredOfItsExpansion_InTheDocumentedOrder(string global, params string[] expansion)
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

        var value = binding.Resolve(new FileData("any"), new Origin(new SourceId(0), "year", 0));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(1991m) }));
        Assert.That(after.Resolutions, Is.Zero);
    }
}
