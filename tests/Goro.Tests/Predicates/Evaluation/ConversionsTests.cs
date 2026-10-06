using Goro.Predicates.Evaluation;
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
    public void StringFromByteCount_IsItsLiteralInBytes()
    {
        Assert.That(Conversions.StringFromByteCount(new ByteCount(1433.6m), out var text), Is.True);
        Assert.That(text, Is.EqualTo("1433.6b"));
    }

    [Test]
    public void StringFromDuration_IsItsLiteralInSeconds()
    {
        Assert.That(Conversions.StringFromDuration(new Duration(120), out var text), Is.True);
        Assert.That(text, Is.EqualTo("120s"));
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

    [TestCase("4:05", 245)]
    [TestCase("4m5s", 245)]
    [TestCase("245", 245)]
    [TestCase("245s", 245)]
    [TestCase("1:00:00", 3600)]
    [TestCase("0", 0)]
    [TestCase("+5", 5)]
    public void DurationFromString_ADurationLiteral_OrWholeSeconds_Converts(string text, decimal seconds)
    {
        Assert.That(Conversions.DurationFromString(text, out var duration), Is.True);
        Assert.That(duration, Is.EqualTo(new Duration(seconds)));
    }

    [TestCase("1.5")]
    [TestCase("-1")]
    [TestCase(" 5")]
    [TestCase("5 ")]
    [TestCase("4m 5s")]
    [TestCase("1kb")]
    [TestCase("abc")]
    [TestCase("")]
    [TestCase("4:61")]
    public void DurationFromString_AnythingElse_DoesNotConvert(string text)
    {
        Assert.That(Conversions.DurationFromString(text, out _), Is.False);
    }

    [TestCase("10kib", 10240)]
    [TestCase("10240", 10240)]
    [TestCase("1.4kib", 1433.6)]
    [TestCase("1433.6", 1433.6)]
    [TestCase("1433.6b", 1433.6)]
    [TestCase("0b", 0)]
    public void ByteCountFromString_ABytecountLiteral_OrBytes_Converts(string text, decimal bytes)
    {
        Assert.That(Conversions.ByteCountFromString(text, out var count), Is.True);
        Assert.That(count, Is.EqualTo(new ByteCount(bytes)));
    }

    [TestCase("-1")]
    [TestCase(" 1")]
    [TestCase("1 kb")]
    [TestCase("5m")]
    [TestCase("x")]
    public void ByteCountFromString_AnythingElse_DoesNotConvert(string text)
    {
        Assert.That(Conversions.ByteCountFromString(text, out _), Is.False);
    }

    [TestCase(245, true)]
    [TestCase(0, true)]
    [TestCase(1.5, false)]
    [TestCase(-1, false)]
    public void DurationFromNumber_IsWholeSecondsNotNegative(decimal number, bool converts)
    {
        Assert.That(Conversions.DurationFromNumber(number, out var duration), Is.EqualTo(converts));
        if (converts)
        {
            Assert.That(duration, Is.EqualTo(new Duration(number)));
        }
    }

    [TestCase(1433.6, true)]
    [TestCase(0, true)]
    [TestCase(-1, false)]
    public void ByteCountFromNumber_IsBytesNotNegative(decimal number, bool converts)
    {
        Assert.That(Conversions.ByteCountFromNumber(number, out _), Is.EqualTo(converts));
    }

    // The canonical literal of a unit names its unit, so it is not a number.
    [TestCase("245s")]
    [TestCase("1000b")]
    public void NumberFromString_OfAUnitsLiteral_DoesNotConvert(string text)
    {
        Assert.That(Conversions.NumberFromString(text, out _), Is.False);
    }
}
