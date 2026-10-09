using Goro.Predicates.Identifiers.Interpretation;

namespace Goro.Tests.Predicates.Identifiers.Interpretation;

/// <summary>
/// The TCON conventions of "Genres" in docs/features/builtins/identifiers.md, and the table of
/// docs/features/builtins/genres.md.
/// </summary>
public class GenresTests
{
    [TestCase("Rock", new[] { "Rock" }, TestName = "free text")]
    [TestCase("Rock; Metal", new[] { "Rock", "Metal" }, TestName = "a semicolon separates, and each part is trimmed")]
    [TestCase("Rock/Metal", new[] { "Rock", "Metal" }, TestName = "a slash separates")]
    [TestCase("17", new[] { "Rock" }, TestName = "a bare number is a reference")]
    [TestCase("(17)", new[] { "Rock" }, TestName = "a reference in parentheses")]
    [TestCase("(17)Post-Rock", new[] { "Rock", "Post-Rock" }, TestName = "a refinement is a value of its own")]
    [TestCase("(17)Rock", new[] { "Rock" }, TestName = "a refinement repeating its reference adds nothing")]
    [TestCase("(17)rock", new[] { "Rock" }, TestName = "a repeat is recognised without regard to case")]
    [TestCase("(67)Psychadelic", new[] { "Psychedelic" }, TestName = "a repeat is recognised under the historical spelling")]
    [TestCase("(40)AlternRock", new[] { "Alternative Rock" }, TestName = "a repeat under another historical spelling")]
    [TestCase("(51)(39)", new[] { "Techno-Industrial", "Noise" }, TestName = "several references")]
    [TestCase("(17)((weird)", new[] { "Rock", "(weird)" }, TestName = "a doubled parenthesis escapes a literal one")]
    [TestCase("((weird)", new[] { "(weird)" }, TestName = "the escape without a reference")]
    [TestCase("(RX)", new[] { "Remix" }, TestName = "RX in parentheses")]
    [TestCase("RX", new[] { "Remix" }, TestName = "RX bare")]
    [TestCase("(CR)", new[] { "Cover" }, TestName = "CR in parentheses")]
    [TestCase("CR", new[] { "Cover" }, TestName = "CR bare")]
    [TestCase("(RX)Club Mix", new[] { "Remix", "Club Mix" }, TestName = "a refinement after RX")]
    [TestCase("(200)", new[] { "(200)" }, TestName = "a reference to no genre is left as recorded")]
    [TestCase("200", new[] { "200" }, TestName = "a bare number to no genre is left as recorded")]
    [TestCase("(200)Something", new[] { "(200)", "Something" }, TestName = "a refinement after a reference to no genre")]
    [TestCase("()", new[] { "()" }, TestName = "empty parentheses are text")]
    [TestCase("(17", new[] { "(17" }, TestName = "an unclosed parenthesis is text")]
    [TestCase("(rock)", new[] { "(rock)" }, TestName = "a word in parentheses is text")]
    [TestCase("17;Rock", new[] { "Rock", "Rock" }, TestName = "two parts naming the same genre are two values")]
    [TestCase(" (17) ", new[] { "Rock" }, TestName = "whitespace around a part")]
    [TestCase("0", new[] { "Blues" }, TestName = "entry 0")]
    [TestCase("017", new[] { "Rock" }, TestName = "leading zeros")]
    [TestCase("Rock;; ;", new[] { "Rock" }, TestName = "empty parts are dropped")]
    [TestCase("", new string[0], TestName = "nothing at all")]
    public void ResolveTcon(string value, string[] expected)
    {
        Assert.That(Genres.ResolveTcon(value), Is.EqualTo(expected));
    }

    [TestCase(0, "Blues")]
    [TestCase(17, "Rock")]
    [TestCase(29, "Jazz-Funk")]
    [TestCase(84, "Fast Fusion")]
    [TestCase(133, "Worldbeat")]
    [TestCase(147, "Synthpop")]
    public void TheTable_GivesTheModernisedNames(int index, string name)
    {
        Assert.That(Genres.Name(index), Is.EqualTo(name));
    }

    [Test]
    public void TheTable_Has148Entries_AndNoneTwice()
    {
        Assert.That(Genres.Table, Has.Length.EqualTo(148));
        Assert.That(Genres.Table, Is.Unique);
        Assert.That(Genres.Name(148), Is.Null);
        Assert.That(Genres.Name(-1), Is.Null);
    }
}
