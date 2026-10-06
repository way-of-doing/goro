using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Values;

public class NumberTextTests
{
    [TestCase("0", "0")]
    [TestCase("2000", "2000")]
    [TestCase("-1", "-1")]
    [TestCase("+5", "5")]
    [TestCase("-.55", "-0.55")]
    [TestCase(".5", "0.5")]
    [TestCase("1.50", "1.5")]
    [TestCase("007", "7")]
    [TestCase("-0", "0")]
    [TestCase("79228162514264337593543950335", "79228162514264337593543950335")]
    public void TryParse_ValidLiteral_ReadsItsValue(string text, string expected)
    {
        Assert.That(NumberText.TryParse(text, out var number), Is.True);
        Assert.That(NumberText.Format(number), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("1.")]
    [TestCase(".")]
    [TestCase("+")]
    [TestCase("1e5")]
    [TestCase("1,000")]
    [TestCase(" 1")]
    [TestCase("1 ")]
    [TestCase("--1")]
    [TestCase("١")]
    public void TryParse_NotTheLiteralSyntax_Fails(string text)
    {
        Assert.That(NumberText.TryParse(text, out _), Is.False);
    }

    // docs/testing.md: more significant digits than a number holds is not a rounded number.
    [TestCase("79228162514264337593543950336")]
    [TestCase("0.12345678901234567890123456789")]
    [TestCase("8.0000000000000000000000000001")]
    public void TryParse_ValueThatCannotBeHeldExactly_Fails(string text)
    {
        Assert.That(NumberText.TryParse(text, out _), Is.False);
    }

    [Test]
    public void TryParse_TrailingZerosBeyondPrecision_StillExact()
    {
        Assert.That(NumberText.TryParse("1.500000000000000000000000000000000", out var number), Is.True);
        Assert.That(number, Is.EqualTo(1.5m));
    }

    // docs/concepts/predicates.md, AS STRING: one canonical form, whatever the value was written as.
    [TestCase("1.50", "1.5")]
    [TestCase(".5", "0.5")]
    [TestCase("+5", "5")]
    [TestCase("-0", "0")]
    [TestCase("-0.000", "0")]
    [TestCase("1000000", "1000000")]
    [TestCase("0.0000001", "0.0000001")]
    public void Format_WritesTheCanonicalForm(string text, string expected)
    {
        var number = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        Assert.That(NumberText.Format(number), Is.EqualTo(expected));
    }

    [TestCase("123.456")]
    [TestCase("-0.0000000000000000000000000001")]
    [TestCase("79228162514264337593543950335")]
    public void Format_ReadsBackAsTheSameValue(string text)
    {
        NumberText.TryParse(text, out var number);

        Assert.That(NumberText.TryParse(NumberText.Format(number), out var again), Is.True);
        Assert.That(again, Is.EqualTo(number));
    }
}
