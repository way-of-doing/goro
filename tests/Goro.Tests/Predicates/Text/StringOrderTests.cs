using System.Text;
using Goro.Predicates.Text;

namespace Goro.Tests.Predicates.Text;

// Prepared strings are ordered by comparing their code points one at a time, a prefix sorting first.
public class StringOrderTests
{
    private static int Compare(string x, string y)
    {
        var order = StringOrder.Normalized;
        return Math.Sign(order.Compare(order.Prepare(x), order.Prepare(y)));
    }

    [Test]
    public void OrdersPlainLettersAsWritten()
    {
        Assert.That(Compare("abc", "abd"), Is.Negative);
        Assert.That(Compare("abd", "abc"), Is.Positive);
    }

    [Test]
    public void OrdersByCodePointRatherThanCodeUnit()
    {
        // U+FFFD is one code unit, 0xFFFD; U+1F600 is two, starting with 0xD83D.
        Assert.That(string.CompareOrdinal("\uFFFD", "\U0001F600"), Is.Positive, "the order of UTF-16 code units");
        Assert.That(Compare("\uFFFD", "\U0001F600"), Is.Negative);
        Assert.That(Compare("\U0001F600", "\uFFFD"), Is.Positive);
        Assert.That(Compare("\uFFFF", "\U00010000"), Is.Negative);
        Assert.That(Compare("x\uE000", "x\U0001F600"), Is.Negative);
    }

    [Test]
    public void OrdersCodePointsAboveTheBmpAmongThemselves()
    {
        Assert.That(Compare("\U0001F600", "\U0001F601"), Is.Negative);
        Assert.That(Compare("\U0001F600", "\U00020000"), Is.Negative);
    }

    [Test]
    public void SortsAPrefixFirst()
    {
        Assert.That(Compare("ab", "abc"), Is.Negative);
        Assert.That(Compare("abc", "ab"), Is.Positive);
        Assert.That(Compare("", "a"), Is.Negative);
    }

    [Test]
    public void EqualStringsCompareEqual()
    {
        Assert.That(Compare("", ""), Is.Zero);
        Assert.That(Compare("Motörhead", "motorhead"), Is.Zero);
        Assert.That(StringOrder.Literal.Compare("\U0001F600", "\U0001F600"), Is.Zero);
    }

    [Test]
    public void ComparesWhatItIsGivenWithoutPreparingIt() =>
        Assert.That(StringOrder.Normalized.Compare("A", "a"), Is.Not.Zero);

    [Test]
    public void ForAMode_GivesThatModesOrder()
    {
        Assert.That(StringOrder.For(ComparisonMode.Normalized), Is.SameAs(StringOrder.Normalized));
        Assert.That(StringOrder.For(ComparisonMode.Literal), Is.SameAs(StringOrder.Literal));
        Assert.That(StringOrder.Literal.Prepare("Motörhead"), Is.EqualTo("Motörhead"));
        Assert.That(StringOrder.Normalized.Prepare("Motörhead"), Is.EqualTo("motorhead"));
    }

    // Code point order is also the order of the bytes of UTF-8, which gives an independent definition
    // to check against.
    [Test]
    public void AgreesWithTheOrderOfUtf8Bytes()
    {
        string[] pieces = ["a", "b", "\u00E9", "\u0800", "\uD7FF", "\uE000", "\uFFFD", "\uFFFF", "\U00010000", "\U0001F600", "\U0010FFFF"];
        var random = new Random(20261005);
        string Generate()
        {
            var text = new StringBuilder();
            for (int i = random.Next(0, 4); i > 0; i--) text.Append(pieces[random.Next(pieces.Length)]);
            return text.ToString();
        }

        for (int n = 0; n < 20_000; n++)
        {
            string x = Generate(), y = Generate();
            int expected = Math.Sign(Encoding.UTF8.GetBytes(x).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y)));
            Assert.That(Math.Sign(StringOrder.Literal.Compare(x, y)), Is.EqualTo(expected), $"\"{x}\" against \"{y}\"");
        }
    }

    // Compare is never given malformed text, which preparation repairs, but it is total all the same.
    [Test]
    public void IsAntisymmetricEvenOnMalformedText()
    {
        string[] texts = ["\uD800", "\uDC00", "\uD800\uDC00", "\uE000", "\uFFFF", "a\uD800", "a", ""];
        foreach (var x in texts)
            foreach (var y in texts)
                Assert.That(Math.Sign(StringOrder.Literal.Compare(x, y)), Is.EqualTo(-Math.Sign(StringOrder.Literal.Compare(y, x))));
    }
}
