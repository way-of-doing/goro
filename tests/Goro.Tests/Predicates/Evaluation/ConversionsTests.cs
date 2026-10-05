using Goro.Predicates.Binding;
using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Evaluation;

public class ConversionsTests
{
    [TestCase("5", 5)]
    [TestCase("-1.25", -1.25)]
    [TestCase(".5", 0.5)]
    [TestCase("+5", 5)]
    public void NumberFromString_NumberLiteral_Converts(string text, decimal expected)
    {
        Assert.That(Conversions.NumberFromString(text, out var number), Is.True);
        Assert.That(number, Is.EqualTo(expected));
    }

    // Not number literals, or not representable exactly: never a rounded number.
    [TestCase("abc")]
    [TestCase("")]
    [TestCase(" 5")]
    [TestCase("1.")]
    [TestCase("1e3")]
    [TestCase("12345678901234567890123456789012")]
    [TestCase("0.000000000000000000000000000001")]
    public void NumberFromString_NotANumberItCanHold_Fails(string text)
    {
        Assert.That(Conversions.NumberFromString(text, out _), Is.False);
    }

    [Test]
    public void NumberFromByteCount_IsBytes()
    {
        Assert.That(Conversions.NumberFromByteCount(new ByteCount(1433.6m), out var number), Is.True);
        Assert.That(number, Is.EqualTo(1433.6m));
    }

    [Test]
    public void NumberFromDuration_IsSeconds()
    {
        Assert.That(Conversions.NumberFromDuration(new Duration(245), out var number), Is.True);
        Assert.That(number, Is.EqualTo(245m));
    }

    [TestCase("1.50", "1.5")]
    [TestCase(".5", "0.5")]
    [TestCase("+5", "5")]
    [TestCase("-0", "0")]
    [TestCase("-12.0300", "-12.03")]
    public void StringFromNumber_IsCanonical(string literal, string expected)
    {
        Assert.That(Conversions.NumberFromString(literal, out var number), Is.True);
        Assert.That(Conversions.StringFromNumber(number, out var text), Is.True);
        Assert.That(text, Is.EqualTo(expected));
    }

    [Test]
    public void StringFromNumber_NegativeZero_IsZero()
    {
        Assert.That(Conversions.StringFromNumber(decimal.Negate(0m), out var text), Is.True);
        Assert.That(text, Is.EqualTo("0"));
    }

    [Test]
    public void StringFromByteCount_IsBytesWithoutUnit()
    {
        Assert.That(Conversions.StringFromByteCount(new ByteCount(1433.6m), out var text), Is.True);
        Assert.That(text, Is.EqualTo("1433.6"));
    }

    [Test]
    public void StringFromDuration_IsSecondsWithoutUnit()
    {
        Assert.That(Conversions.StringFromDuration(new Duration(120), out var text), Is.True);
        Assert.That(text, Is.EqualTo("120"));
    }

    [TestCase("1.5")]
    [TestCase("-0.001")]
    [TestCase("79228162514264337593543950335")]
    [TestCase("0.0000000000000000000000000001")]
    public void StringFromNumber_ReadsBackAsTheSameNumber(string literal)
    {
        var number = decimal.Parse(literal, System.Globalization.CultureInfo.InvariantCulture);
        Conversions.StringFromNumber(number, out var text);

        Assert.That(Conversions.NumberFromString(text, out var readBack), Is.True);
        Assert.That(readBack, Is.EqualTo(number));
    }
}
