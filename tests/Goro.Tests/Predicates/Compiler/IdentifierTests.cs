using Goro.Messages;
using Goro.Predicates.Binding;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using static Goro.Tests.Predicates.Support.CompileAssert;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>
/// Resolving identifiers and source functions against the catalog: what names one thing, what names
/// nothing, and what is offered instead.
/// </summary>
public class IdentifierTests
{
    // ---------------------------------------------------------------------------------------------
    // Identifiers

    [TestCase("x == 1")]
    [TestCase("X == 1")]
    public void ConceptWithoutASource_HoweverCased_Resolves(string text)
    {
        var predicate = Compiles(text);

        Assert.That(predicate.Sources.CanonicalForms, Is.EqualTo(new[] { "x" }));
    }

    [TestCase("file::size > 1")]
    [TestCase("FILE::SIZE > 1")]
    [TestCase("File::Size > 1")]
    public void QualifiedIdentifier_HoweverCased_Resolves(string text)
    {
        var predicate = Compiles(text);

        Assert.That(predicate.Root, Is.TypeOf<ComparisonTest<ByteCount>>());
        Assert.That(predicate.Sources.CanonicalForms, Is.EqualTo(new[] { "file::size" }));
    }

    [TestCase("artst == \"x\"", "artist == \"x\"")]
    [TestCase("ARTST == \"x\"", "artist == \"x\"")]
    [TestCase("yaer == 1", "year == 1")]
    [TestCase("file::sise > 1", "file::size > 1")]
    public void UnknownIdentifier_OffersTheClosest(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.UnknownIdentifier));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void UnknownIdentifier_InASource_NamesTheSource()
    {
        var error = Error("id3v1::bpm == 1");

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.UnknownIdentifier));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.UnknownIdentifierInSource(new Code("id3v1"), new Code("bpm"))));
        Assert.That(Marked("id3v1::bpm == 1", error), Is.EqualTo("id3v1::bpm"));
    }

    [TestCase("id3v1::size == 1", "size")]
    [TestCase("file::artist == \"x\"", "artist")]
    [TestCase("vorbis::description == \"x\"", "description")]
    public void ConceptTheSourceCannotSupply_IsAnError(string text, string name)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.UnknownIdentifier));
        Assert.That(error.Message, Is.InstanceOf<ErrorMessage.UnknownIdentifierInSource>());
        Assert.That(((ErrorMessage.UnknownIdentifierInSource)error.Message).Name, Is.EqualTo(new Code(name)));
    }

    [TestCase("zzzzzz == 1")]
    [TestCase("qqqq == 1")]
    public void UnknownIdentifier_FarFromEveryName_OffersNothing(string text)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.UnknownIdentifier));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [TestCase("vorbs::artist == \"x\"", "vorbis::artist == \"x\"")]
    [TestCase("fil::size > 1", "file::size > 1")]
    [TestCase("vorbs::field(\"MOOD\") == \"x\"", "vorbis::field(\"MOOD\") == \"x\"")]
    public void UnknownSource_OffersTheClosest(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.UnknownSource));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void UnknownSource_FarFromEverySource_OffersNothing()
    {
        var error = Error("musicbrainz::id == \"x\"");

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.UnknownSource));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.UnknownSource(new Code("musicbrainz"))));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [TestCase("size > 1", "file::size > 1")]
    [TestCase("Duration > 1", "file::Duration > 1")]
    public void FileConceptWithoutItsSource_OffersIt(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.NeedsSource));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void SourceFunction_WrittenWithoutArguments_SaysSo()
    {
        var error = Error("vorbis::field == \"x\"");

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.SourceFunctionNeedsArguments));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.SourceFunctionNeedsArguments(new Code("vorbis::field"))));
    }

    [Test]
    public void TwoMisspellings_AreTwoErrors_InTextOrder()
    {
        const string text = "artst == \"x\" AND yer == 1";

        var errors = Errors(text);

        Assert.That(errors.Select(e => Marked(text, e)), Is.EqualTo(new[] { "artst", "yer" }));
        Assert.That(errors.Select(e => e.Code), Is.All.EqualTo(SemanticDiagnosticCodes.UnknownIdentifier));
    }

    // The error type: an identifier that names nothing has no type, so nothing it meets complains.
    [TestCase("artst == 1")]
    [TestCase("artst != \"x\"")]
    [TestCase("artst AS NUMBER > 1")]
    [TestCase("artst BETWEEN 1..2")]
    [TestCase("artst =~ r\"x\"")]
    [TestCase("LITERALLY(artst) == 1")]
    [TestCase("artst AND x > 1")]
    [TestCase("NOT artst")]
    [TestCase("artst")]
    [TestCase("FALLBACK(artst, 0) == \"x\"")]
    public void UnknownIdentifier_SetsOffNoFurtherErrors(string text)
    {
        Assert.That(Codes(text), Is.EqualTo(new[] { SemanticDiagnosticCodes.UnknownIdentifier }));
    }

    // ---------------------------------------------------------------------------------------------
    // Source functions

    [TestCase("vorbis::field(\"MOOD\") == \"calm\"", "vorbis::field(\"mood\")")]
    [TestCase("VORBIS::FIELD(\"mood\") == \"calm\"", "vorbis::field(\"mood\")")]
    [TestCase("ape::field(\"Album Artist\") == \"x\"", "ape::field(\"album artist\")")]
    [TestCase("ape::field(r\"Album Artist\") == \"x\"", "ape::field(\"album artist\")")]
    [TestCase("ape::field((\"Album Artist\")) == \"x\"", "ape::field(\"album artist\")")]
    [TestCase("id3v2::field(\"TXXX\", \"MOOD\") == \"calm\"", "id3v2::field(\"txxx\", \"mood\")")]
    [TestCase("id3v2::field(\"tit2\") == \"x\"", "id3v2::field(\"tit2\")")]
    [TestCase("id3v2::field(\"COMM\", \"\") == \"x\"", "id3v2::field(\"comm\", \"\")")]
    public void SourceFunction_WithStringLiterals_ReadsAString(string text, string form)
    {
        var predicate = Compiles(text);

        Assert.That(predicate.Root, Is.TypeOf<ComparisonTest<string>>());
        Assert.That(predicate.Sources.CanonicalForms, Is.EqualTo(new[] { form }));
    }

    [TestCase("id3v2::bytes(\"APIC\") IS ABSENT")]
    [TestCase("ape::bytes(\"Cover Art (Front)\") IS USABLE")]
    [TestCase("COUNT(vorbis::bytes(\"METADATA_BLOCK_PICTURE\")) > 1")]
    [TestCase("PREFERRED(ape::bytes(\"Cover Art (Front)\"), id3v2::bytes(\"APIC\")) IS ABSENT")]
    [TestCase("ALL(id3v2::bytes(\"PRIV\")) IS USABLE")]
    public void Bytes_ReadsABlob_ThatCanBeCountedAndStateTested(string text)
    {
        Compiles(text);
    }

    // field() can be absent and hold several occurrences, from every source that has it.
    [TestCase("vorbis::field(\"X\") != \"y\"")]
    [TestCase("ape::field(\"X\") != \"y\"")]
    [TestCase("id3v2::field(\"TIT2\") != \"y\"")]
    public void Field_IsNeverExactlyOne(string text)
    {
        Assert.That(Error(text).Code, Is.EqualTo(SemanticDiagnosticCodes.AmbiguousNotEqual));
    }

    [Test]
    public void SourceFunction_WithoutASource_SaysItNeedsOne()
    {
        var error = Error("field(\"MOOD\") == \"x\"");

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.NeedsSource));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.SourceFunctionNeedsSource(new Code("field"))));
    }

    [TestCase("ape::count(genre) > 1", "COUNT", "count(genre) > 1")]
    [TestCase("vorbis::feild(\"MOOD\") == \"x\"", "FEILD", "vorbis::field(\"MOOD\") == \"x\"")]
    public void NotASourceFunctionOfTheSource_OffersWhatWasMeant(string text, string _, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.UnknownFunction));
        Assert.That(error.Message, Is.InstanceOf<ErrorMessage.UnknownSourceFunction>());
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("id3v1::field(\"x\") == \"y\"")]
    [TestCase("file::field(\"x\") == \"y\"")]
    public void ASourceWithoutSourceFunctions_HasNone(string text)
    {
        Assert.That(Error(text).Message, Is.InstanceOf<ErrorMessage.UnknownSourceFunction>());
    }

    [TestCase("vorbis::field() == \"x\"", 1, 1)]
    [TestCase("vorbis::field(\"a\", \"b\") == \"x\"", 1, 1)]
    [TestCase("id3v2::field(\"TXXX\", \"a\", \"b\") == \"x\"", 1, 2)]
    public void SourceFunction_GivenTheWrongNumberOfArguments_SaysHowMany(string text, int minimum, int maximum)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.WrongArgumentCount));
        Assert.That(error.Message, minimum == maximum
            ? Is.InstanceOf<ErrorMessage.WrongArgumentCount>()
            : Is.InstanceOf<ErrorMessage.WrongArgumentCountBetween>());
    }

    [TestCase("vorbis::field(artist) == \"x\"", "artist")]
    [TestCase("vorbis::field(5) == \"x\"", "5")]
    [TestCase("vorbis::field(\"MO\" AS STRING) == \"x\"", "\"MO\" AS STRING")]
    [TestCase("id3v2::field(\"TXXX\", x) == \"y\"", "x")]
    public void SourceFunction_GivenAnythingButAStringLiteral_IsAnError(string text, string argument)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.ArgumentNotLiteral));
        Assert.That(Marked(text, error), Is.EqualTo(argument));
    }

    [TestCase("id3v2::field(\"TYER\") == \"x\"", "id3v2::field(\"TDRC\") == \"x\"")]
    [TestCase("id3v2::field(\"tt2\") == \"x\"", "id3v2::field(\"TIT2\") == \"x\"")]
    [TestCase("id3v2::bytes(\"PIC\") IS ABSENT", null)]
    public void RenamedFrame_OffersTheIdentifierItIsReadUnder(string text, string? rewrite)
    {
        if (rewrite is null)
        {
            Compiles(text);
            return;
        }

        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(SemanticDiagnosticCodes.ArgumentRejected));
        Assert.That(error.Message, Is.InstanceOf<ErrorMessage.FrameRenamed>());
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void FieldOfAFrameThatHoldsNoText_OffersBytes()
    {
        const string text = "id3v2::field(\"APIC\") IS ABSENT";

        var error = Error(text);

        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.FrameNotText(new Code("APIC"))));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { "id3v2::bytes(\"APIC\") IS ABSENT" }));
    }

    [Test]
    public void DescriptionForAFrameWithoutOne_OffersTheCallWithoutIt()
    {
        const string text = "id3v2::field(\"TIT2\", \"x\") == \"y\"";

        var error = Error(text);

        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.DescriptionNotTaken(new Code("TIT2"))));
        Assert.That(Marked(text, error), Is.EqualTo("\"x\""));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { "id3v2::field(\"TIT2\") == \"y\"" }));
    }

    [TestCase("TI")]
    [TestCase("TIT22")]
    [TestCase("T-T2")]
    [TestCase("")]
    public void MalformedFrameIdentifier_IsAnError(string frame)
    {
        var error = Error($"id3v2::field(\"{frame}\") == \"x\"");

        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.MalformedFrame(new Code(frame))));
    }

    // A call that cannot be resolved keeps its function's type, so what it is compared with is
    // still checked, and a mismatch beside it reported.
    [Test]
    public void RejectedSourceCall_KeepsItsType()
    {
        Assert.That(Codes("id3v2::field(\"TYER\") == 1"),
            Is.EquivalentTo(new[] { SemanticDiagnosticCodes.ArgumentRejected, SemanticDiagnosticCodes.TypeMismatch }));
    }

    // ---------------------------------------------------------------------------------------------
    // What the grammar no longer has

    [TestCase("::artist == \"x\"", "artist == \"x\"")]
    [TestCase("::file::size > 1", "file::size > 1")]
    [TestCase("ape::\"Album Artist\" == \"x\"", "ape::field(\"Album Artist\") == \"x\"")]
    public void OldSpellings_AreAnsweredWithTheNewOnes(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("id3v2::track::x == 1", SyntaxDiagnosticCodes.NestedIdentifier)]
    [TestCase("id3v2::raw::TRCK == \"1\"", SyntaxDiagnosticCodes.NestedIdentifier)]
    [TestCase("and::x == 1", SyntaxDiagnosticCodes.ReservedWordInIdentifier)]
    [TestCase("ape::and == 1", SyntaxDiagnosticCodes.ReservedWordInIdentifier)]
    [TestCase("NOT::x == 1", SyntaxDiagnosticCodes.RootedIdentifier)]
    public void OldSpellings_WithNothingToOffer_AreErrors(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }
}
