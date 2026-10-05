namespace Goro.Predicates.Text;

/// <summary>
/// The Unicode property <c>Default_Ignorable_Code_Point</c>, which .NET does not expose: the
/// characters normalized mode removes in step 3.
/// </summary>
/// <remarks>
/// Taken from <c>DerivedCoreProperties.txt</c> of Unicode 16.0.0, the version of .NET 10's own
/// character tables; the property is identical in Unicode 17.0.0. Lines of that file that are
/// adjacent are merged here, so <c>180B..180D</c>, <c>180E</c> and <c>180F</c> appear as one range.
/// The property is not guaranteed stable, so a later Unicode version should be checked against it.
/// </remarks>
internal static class DefaultIgnorable
{
    private static readonly (int First, int Last)[] Ranges =
    [
        (0x00AD, 0x00AD),   // SOFT HYPHEN
        (0x034F, 0x034F),   // COMBINING GRAPHEME JOINER
        (0x061C, 0x061C),   // ARABIC LETTER MARK
        (0x115F, 0x1160),   // HANGUL CHOSEONG FILLER, HANGUL JUNGSEONG FILLER
        (0x17B4, 0x17B5),   // KHMER VOWEL INHERENT AQ, AA
        (0x180B, 0x180F),   // MONGOLIAN FREE VARIATION SELECTORS, MONGOLIAN VOWEL SEPARATOR
        (0x200B, 0x200F),   // ZERO WIDTH SPACE .. RIGHT-TO-LEFT MARK
        (0x202A, 0x202E),   // bidirectional embeddings and overrides
        (0x2060, 0x206F),   // WORD JOINER .. NOMINAL DIGIT SHAPES, with reserved U+2065
        (0x3164, 0x3164),   // HANGUL FILLER
        (0xFE00, 0xFE0F),   // VARIATION SELECTOR-1 .. 16
        (0xFEFF, 0xFEFF),   // ZERO WIDTH NO-BREAK SPACE, the byte order mark
        (0xFFA0, 0xFFA0),   // HALFWIDTH HANGUL FILLER
        (0xFFF0, 0xFFF8),   // reserved
        (0x1BCA0, 0x1BCA3), // SHORTHAND FORMAT LETTER OVERLAP .. UP STEP
        (0x1D173, 0x1D17A), // MUSICAL SYMBOL BEGIN BEAM .. END PHRASE
        (0xE0000, 0xE0FFF), // tags, VARIATION SELECTOR-17 .. 256, and reserved
    ];

    public static bool Contains(int codePoint)
    {
        // Ranges are in ascending order, so the scan stops at the first one that starts later.
        foreach (var (first, last) in Ranges)
        {
            if (codePoint < first) return false;
            if (codePoint <= last) return true;
        }

        return false;
    }
}
