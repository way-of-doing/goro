using System.Text;

namespace Goro.Predicates.Text;

/// <summary>
/// Step 5 of normalized mode: each character becomes the lowercase form of its uppercase form,
/// by the simple, one-to-one Unicode case mappings and no language-specific rules.
/// </summary>
internal static class CaseFolding
{
    // The fold of every BMP code unit, computed once, on the first string that is not ASCII. Asking
    // .NET for each character as it comes would cost two calls into ICU per character; asking for
    // all 65,536 at once costs two calls in all: under half a millisecond, or a few milliseconds when
    // it is also the first thing in the run to need ICU's case mappings.
    private static readonly char[] Bmp = BuildBmpTable();

    public static Rune Fold(Rune rune) =>
        rune.IsBmp ? new Rune(Bmp[rune.Value]) : Rune.ToLowerInvariant(Rune.ToUpperInvariant(rune));

    private static char[] BuildBmpTable()
    {
        // Surrogates have no case and are never folded; they are blanked out so that two adjacent
        // ones cannot be read as a pair.
        var characters = new char[0x10000];
        for (int c = 0; c < characters.Length; c++)
            characters[c] = char.IsSurrogate((char)c) ? '\0' : (char)c;

        // Case mapping preserves length, and no simple mapping crosses between the BMP and the
        // planes above it, so position c of the result is the fold of character c.
        var upper = new char[characters.Length];
        var folded = new char[characters.Length];
        characters.AsSpan().ToUpperInvariant(upper);
        upper.AsSpan().ToLowerInvariant(folded);

        for (int c = 0xD800; c <= 0xDFFF; c++)
            folded[c] = (char)c;

        // .NET's invariant casing keeps the Turkish i's to themselves, for compatibility with Windows,
        // where Unicode maps dotless ı to I and dotted İ to i. The specification asks for Unicode's.
        // (İ never reaches this step, decomposition having made it I followed by a dot above.)
        folded['ı'] = 'i';
        folded['İ'] = 'i';

        return folded;
    }
}
