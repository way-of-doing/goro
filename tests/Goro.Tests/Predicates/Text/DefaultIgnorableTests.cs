using Goro.Predicates.Text;

namespace Goro.Tests.Predicates.Text;

// Step 3 of normalized mode removes every code point with the property Default_Ignorable_Code_Point.
// The property's ranges are written out here as DerivedCoreProperties.txt gives them (Unicode 16.0.0,
// unchanged in 17.0.0), and every code point in them is checked, along with the code points either
// side of each range. Each is put between two Cyrillic letters, so that a mark is not removed by
// step 4 instead.
public class DefaultIgnorableTests
{
    private static readonly (int First, int Last)[] UnicodeRanges =
    [
        (0x00AD, 0x00AD), (0x034F, 0x034F), (0x061C, 0x061C), (0x115F, 0x1160), (0x17B4, 0x17B5),
        (0x180B, 0x180D), (0x180E, 0x180E), (0x180F, 0x180F), (0x200B, 0x200F), (0x202A, 0x202E),
        (0x2060, 0x2064), (0x2065, 0x2065), (0x2066, 0x206F), (0x3164, 0x3164), (0xFE00, 0xFE0F),
        (0xFEFF, 0xFEFF), (0xFFA0, 0xFFA0), (0xFFF0, 0xFFF8), (0x1BCA0, 0x1BCA3), (0x1D173, 0x1D17A),
        (0xE0000, 0xE0000), (0xE0001, 0xE0001), (0xE0002, 0xE001F), (0xE0020, 0xE007F),
        (0xE0080, 0xE00FF), (0xE0100, 0xE01EF), (0xE01F0, 0xE0FFF),
    ];

    private static bool IsIgnorable(int codePoint) => UnicodeRanges.Any(r => codePoint >= r.First && codePoint <= r.Last);

    private static string Between(int codePoint) => "\u0436" + char.ConvertFromUtf32(codePoint) + "\u0436";

    private static string Prepare(string text) => Normalization.Prepare(text, ComparisonMode.Normalized);

    [Test]
    public void RemovesEveryDefaultIgnorableCodePoint()
    {
        foreach (var (first, last) in UnicodeRanges)
            for (int c = first; c <= last; c++)
                Assert.That(Prepare(Between(c)), Is.EqualTo("\u0436\u0436"), $"U+{c:X4}");
    }

    [Test]
    public void KeepsTheCodePointsEitherSideOfEachRange()
    {
        var neighbours = UnicodeRanges.SelectMany(r => new[] { r.First - 1, r.Last + 1 }).Where(c => !IsIgnorable(c));
        foreach (var c in neighbours)
            Assert.That(Prepare(Between(c)), Is.Not.EqualTo("\u0436\u0436"), $"U+{c:X4}");
    }

    [Test]
    public void LiteralModeKeepsThem()
    {
        foreach (var (first, _) in UnicodeRanges)
            Assert.That(Normalization.Prepare(Between(first), ComparisonMode.Literal), Is.EqualTo(Between(first)), $"U+{first:X4}");
    }
}
