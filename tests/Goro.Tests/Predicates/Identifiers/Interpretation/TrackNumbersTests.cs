using Goro.Predicates.Identifiers.Interpretation;

namespace Goro.Tests.Predicates.Identifiers.Interpretation;

/// <summary>"Parsing track numbers" in docs/features/builtins/identifiers.md.</summary>
public class TrackNumbersTests
{
    [TestCase("3", 3)]
    [TestCase("03", 3)]
    [TestCase("0", 0)]
    [TestCase("3/12", 3)]
    [TestCase("12/3", 12)]
    public void AnUnsignedNumber_OrTheFirstOfTwo_IsTheTrack(string text, int expected)
    {
        Assert.That(TrackNumbers.Parse(text), Is.EqualTo(expected));
    }

    [TestCase("", TestName = "nothing")]
    [TestCase("three", TestName = "a word")]
    [TestCase("-3", TestName = "a sign")]
    [TestCase("+3", TestName = "a plus sign")]
    [TestCase("3.0", TestName = "a decimal point")]
    [TestCase("3 / 12", TestName = "whitespace around the slash")]
    [TestCase("3/", TestName = "nothing after the slash")]
    [TestCase("/12", TestName = "nothing before the slash")]
    [TestCase("3/12/1", TestName = "two slashes")]
    [TestCase("3/x", TestName = "a word after the slash")]
    [TestCase("٣", TestName = "a digit that is not ASCII")]
    [TestCase("99999999999999999999999999999999", TestName = "a number too large to hold")]
    public void AnythingElse_IsUnusable(string text)
    {
        Assert.That(TrackNumbers.Parse(text), Is.Null);
    }
}
