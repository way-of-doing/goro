using Goro.Messages;
using Goro.Predicates.Binding;
using static Goro.Tests.Predicates.Binding.Support.BindAssert;

namespace Goro.Tests.Predicates.Binding;

/// <summary>
/// Resolving identifiers against the catalog: what names one identifier, what names none, and the
/// closest name offered for a misspelling.
/// </summary>
public class IdentifierBindingTests
{
    [TestCase("x == 1")]
    [TestCase("X == 1")]
    [TestCase("::x == 1")]
    [TestCase("::\"x\" == 1")]
    [TestCase("::\"X\" == 1")]
    public void GlobalIdentifier_HoweverWritten_Resolves(string text)
    {
        var predicate = Compiles(text);

        Assert.That(predicate.Sources.CanonicalForms, Is.EqualTo(new[] { "x" }));
    }

    [TestCase("file::size > 1")]
    [TestCase("FILE::SIZE > 1")]
    [TestCase("::file::size > 1")]
    [TestCase("file::\"size\" > 1")]
    [TestCase("File::\"Size\" > 1")]
    public void QualifiedIdentifier_HoweverWritten_Resolves(string text)
    {
        var predicate = Compiles(text);

        Assert.That(predicate.Root, Is.TypeOf<ComparisonTest<Goro.Predicates.Values.ByteCount>>());
        Assert.That(predicate.Sources.CanonicalForms, Is.EqualTo(new[] { "file::size" }));
    }

    [TestCase("vorbis::bpm == \"120\"")]
    [TestCase("vorbis::\"Album Artist\" == \"x\"")]
    [TestCase("id3v2::raw::TRCK == \"1\"")]
    public void OpenNamespace_AcceptsAnyName(string text)
    {
        Compiles(text);
    }

    [TestCase("artst == \"x\"", "artist == \"x\"")]
    [TestCase("ARTST == \"x\"", "artist == \"x\"")]
    [TestCase("::artst == \"x\"", "::artist == \"x\"")]
    [TestCase("yaer == 1", "year == 1")]
    [TestCase("file::sise > 1", "file::size > 1")]
    [TestCase("file::\"sise\" > 1", "file::size > 1")]
    public void UnknownIdentifier_OffersTheClosest(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(BinderDiagnosticCodes.UnknownIdentifier));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void UnknownIdentifier_InAClosedNamespace_NamesTheNamespace()
    {
        var error = Error("id3v1::bpm == 1");

        Assert.That(error.Code, Is.EqualTo(BinderDiagnosticCodes.UnknownIdentifier));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.UnknownIdentifierInNamespace(new Code("id3v1"), new Code("bpm"))));
        Assert.That(Marked("id3v1::bpm == 1", error), Is.EqualTo("id3v1::bpm"));
    }

    [TestCase("zzzzzz == 1")]
    [TestCase("qqqq == 1")]
    public void UnknownIdentifier_FarFromEveryName_OffersNothing(string text)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(BinderDiagnosticCodes.UnknownIdentifier));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [TestCase("vorbs::artist == \"x\"", "vorbis::artist == \"x\"")]
    [TestCase("id3v2::rav::TIT2 == \"x\"", "id3v2::raw::TIT2 == \"x\"")]
    [TestCase("::fil::size > 1", "::file::size > 1")]
    public void UnknownNamespace_OffersTheClosest(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(BinderDiagnosticCodes.UnknownNamespace));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void UnknownNamespace_FarFromEveryNamespace_OffersNothing()
    {
        var error = Error("musicbrainz::id == \"x\"");

        Assert.That(error.Code, Is.EqualTo(BinderDiagnosticCodes.UnknownNamespace));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.UnknownNamespace(new Code("musicbrainz"))));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [Test]
    public void TwoMisspellings_AreTwoErrors_InTextOrder()
    {
        const string text = "artst == \"x\" AND yer == 1";

        var errors = Errors(text);

        Assert.That(errors.Select(e => Marked(text, e)), Is.EqualTo(new[] { "artst", "yer" }));
        Assert.That(errors.Select(e => e.Code), Is.All.EqualTo(BinderDiagnosticCodes.UnknownIdentifier));
    }

    // The error type: an identifier that names nothing has no type, so nothing it meets complains.
    [TestCase("artst == 1")]
    [TestCase("artst != \"x\"")]
    [TestCase("NUMBER(artst) > 1")]
    [TestCase("artst BETWEEN 1..2")]
    [TestCase("artst =~ r\"x\"")]
    [TestCase("LITERALLY(artst) == 1")]
    [TestCase("artst AND x > 1")]
    [TestCase("NOT artst")]
    [TestCase("artst")]
    [TestCase("FALLBACK(artst, 0) == \"x\"")]
    public void UnknownIdentifier_SetsOffNoFurtherErrors(string text)
    {
        Assert.That(Codes(text), Is.EqualTo(new[] { BinderDiagnosticCodes.UnknownIdentifier }));
    }
}
