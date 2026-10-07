using Goro.Predicates.Identifiers;

namespace Goro.Tests.Predicates.Identifiers;

/// <summary>
/// The names of what a predicate reads, which decide what is one warning source: an identifier, and
/// a call of a source function.
/// </summary>
public class DeclaredNameTests
{
    [Test]
    public void AnIdentifier_IgnoresCaseInBothParts()
    {
        var written = new IdentifierName("id3v2", "artist");
        var shouted = new IdentifierName("ID3V2", "ARTIST");

        Assert.That(shouted, Is.EqualTo(written));
        Assert.That(shouted.GetHashCode(), Is.EqualTo(written.GetHashCode()));
    }

    [Test]
    public void AnIdentifier_WithAndWithoutASource_AreDifferent()
    {
        Assert.That(new IdentifierName("id3v2", "artist"), Is.Not.EqualTo(IdentifierName.Plain("artist")));
        Assert.That(new IdentifierName("id3v2", "artist"), Is.Not.EqualTo(new IdentifierName("id3v1", "artist")));
    }

    [TestCase(null, "artist", "artist")]
    [TestCase("file", "size", "file::size")]
    public void AnIdentifier_IsWrittenWithItsSource(string? source, string name, string expected)
    {
        Assert.That(new IdentifierName(source, name).ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void ACall_IgnoresCaseInEveryPart_ArgumentsIncluded()
    {
        var written = new SourceCallName("vorbis", "field", ["mood"]);
        var shouted = new SourceCallName("VORBIS", "FIELD", ["MOOD"]);

        Assert.That(shouted, Is.EqualTo(written));
        Assert.That(shouted.GetHashCode(), Is.EqualTo(written.GetHashCode()));
    }

    [Test]
    public void ACall_WithOtherArguments_IsDifferent()
    {
        var all = new SourceCallName("id3v2", "field", ["TXXX"]);

        Assert.That(new SourceCallName("id3v2", "field", ["TXXX", "MOOD"]), Is.Not.EqualTo(all));
        Assert.That(new SourceCallName("id3v2", "bytes", ["TXXX"]), Is.Not.EqualTo(all));
    }

    [Test]
    public void ACall_AndAnIdentifier_AreNeverEqual()
    {
        Assert.That(new SourceCallName("vorbis", "field", ["artist"]), Is.Not.EqualTo((DeclaredName)new IdentifierName("vorbis", "artist")));
    }

    [Test]
    public void ACall_IsWrittenWithItsArgumentsQuoted()
    {
        Assert.That(new SourceCallName("id3v2", "field", ["TXXX", "say \"hi\""]).ToString(),
            Is.EqualTo("id3v2::field(\"TXXX\", \"say \\\"hi\\\"\")"));
    }
}
