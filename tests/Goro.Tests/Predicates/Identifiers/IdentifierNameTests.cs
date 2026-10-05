using Goro.Predicates.Identifiers;

namespace Goro.Tests.Predicates.Identifiers;

public class IdentifierNameTests
{
    [Test]
    public void Equals_IgnoresCaseInEveryPart()
    {
        var written = new IdentifierName(["id3v2"], "tit2");
        var shouted = new IdentifierName(["ID3V2"], "TIT2");

        Assert.That(shouted, Is.EqualTo(written));
        Assert.That(shouted.GetHashCode(), Is.EqualTo(written.GetHashCode()));
    }

    [Test]
    public void Equals_QuotedPartWithSpaces_IgnoresCase()
    {
        Assert.That(new IdentifierName(["ape"], "Album Artist"), Is.EqualTo(new IdentifierName(["APE"], "album artist")));
    }

    [Test]
    public void Equals_DifferentNamespace_IsDifferent()
    {
        Assert.That(new IdentifierName(["id3v1"], "artist"), Is.Not.EqualTo(new IdentifierName(["id3v1", "raw"], "artist")));
        Assert.That(new IdentifierName(["id3v1"], "artist"), Is.Not.EqualTo(IdentifierName.Global("artist")));
    }

    [TestCase(new string[0], "artist", "artist")]
    [TestCase(new[] { "id3v1", "raw" }, "genre", "id3v1::raw::genre")]
    [TestCase(new[] { "ape" }, "Album Artist", "ape::\"Album Artist\"")]
    [TestCase(new[] { "ape" }, "say \"hi\"\\", "ape::\"say \\\"hi\\\"\\\\\"")]
    public void ToString_QuotesOnlyWhatABareNameCannotSpell(string[] @namespace, string name, string expected)
    {
        Assert.That(new IdentifierName(@namespace, name).ToString(), Is.EqualTo(expected));
    }
}
