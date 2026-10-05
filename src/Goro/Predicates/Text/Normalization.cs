// Owned by the normalization group (G1) of the predicate-runtime-architecture line.
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Goro.Predicates.Text;

/// <summary>Prepares strings for comparison, exactly as docs/concepts/normalization.md defines.</summary>
/// <remarks>
/// The steps of normalized mode are numbered here as they are in the specification. Preparing a
/// string that has already been prepared is not guaranteed to change nothing, so each datum is
/// to be prepared exactly once.
/// </remarks>
public static class Normalization
{
    private const char ReplacementCharacter = '\uFFFD';
    private const int CombiningDiaeresis = 0x0308;

    // Below U+0300 there are no combining marks and no surrogates, so text made only of such
    // characters is already in NFC.
    private const char FirstCharacterNfcCanChange = '\u0300';

    // Step 6 at most doubles a character (ß becomes ss); a result up to this long is built on the stack.
    private const int StackBufferLength = 256;

    /// <summary>Total: never throws, however malformed the text.</summary>
    public static string Prepare(string text, ComparisonMode mode) =>
        mode == ComparisonMode.Literal ? PrepareLiteral(text) : PrepareNormalized(text);

    private static string PrepareLiteral(string text)
    {
        if (!text.AsSpan().ContainsAnyInRange(FirstCharacterNfcCanChange, char.MaxValue)) return text;

        return UnicodeForms.Normalize(RepairSurrogates(text), NormalizationForm.FormC);
    }

    private static string PrepareNormalized(string text)
    {
        // Tag text is overwhelmingly ASCII, and for ASCII every step but case folding does nothing.
        if (Ascii.IsValid(text)) return LowercaseAscii(text);

        string decomposed = UnicodeForms.Normalize(RepairSurrogates(text), NormalizationForm.FormKD);   // steps 1 and 2

        char[]? rented = null;
        Span<char> buffer = decomposed.Length * 2 <= StackBufferLength
            ? stackalloc char[decomposed.Length * 2]
            : rented = ArrayPool<char>.Shared.Rent(decomposed.Length * 2);
        try
        {
            var folded = buffer[..RemoveAndFold(decomposed, buffer)];   // steps 3 to 6
            return folded.ContainsAnyInRange(FirstCharacterNfcCanChange, char.MaxValue)
                ? UnicodeForms.Normalize(new string(folded), NormalizationForm.FormC)   // step 7
                : new string(folded);
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Step 1: every surrogate that is not part of a pair becomes U+FFFD.</summary>
    private static string RepairSurrogates(string text)
    {
        // Surrogates are rare in tag text, and looking for the first one is a vectorized scan.
        int first = text.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF');
        if (first < 0 || !HasUnpairedSurrogate(text.AsSpan(first))) return text;

        return string.Create(text.Length, text, static (repaired, original) =>
        {
            original.CopyTo(repaired);
            for (int i = 0; i < repaired.Length; i++)
            {
                if (StartsSurrogatePair(repaired, i)) i++;
                else if (char.IsSurrogate(repaired[i])) repaired[i] = ReplacementCharacter;
            }
        });
    }

    private static bool HasUnpairedSurrogate(ReadOnlySpan<char> text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (StartsSurrogatePair(text, i)) i++;
            else if (char.IsSurrogate(text[i])) return true;
        }

        return false;
    }

    private static bool StartsSurrogatePair(ReadOnlySpan<char> text, int i) =>
        char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);

    /// <summary>
    /// Steps 3 to 6 in a single pass, taking each character through the four steps in order. Since
    /// a removed default-ignorable character is skipped before it can become the base a mark looks
    /// back to, this is exactly what applying the steps one after another would do.
    /// </summary>
    private static int RemoveAndFold(ReadOnlySpan<char> decomposed, Span<char> output)
    {
        int length = 0;
        bool afterLatinOrGreek = false, afterCyrillicE = false;

        foreach (var rune in decomposed.EnumerateRunes())
        {
            if (DefaultIgnorable.Contains(rune.Value)) continue;   // step 3

            if (IsCombiningMark(rune))                              // step 4
            {
                if (afterLatinOrGreek) continue;
                if (rune.Value == CombiningDiaeresis && afterCyrillicE) continue;
            }
            else
            {
                // The nearest character before a mark that is not itself a mark, as it stood before
                // case folding.
                afterLatinOrGreek = IsLatinOrGreek(rune.Value);
                afterCyrillicE = rune.Value is '\u0435' or '\u0415';   // Cyrillic е and Е, not Latin e and E
            }

            length += AppendFoldedLetter(CaseFolding.Fold(rune), output[length..]);   // steps 5 and 6
        }

        return length;
    }

    private static bool IsCombiningMark(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>The letters whose accents step 4 removes.</summary>
    private static bool IsLatinOrGreek(int codePoint) => codePoint is
        (>= 0x0041 and <= 0x005A) or (>= 0x0061 and <= 0x007A) or (>= 0x00C0 and <= 0x02AF)
        or (>= 0x0370 and <= 0x03FF) or (>= 0x1D00 and <= 0x1DBF) or (>= 0x1E00 and <= 0x1FFF)
        or (>= 0x2C60 and <= 0x2C7F) or (>= 0xA720 and <= 0xA7FF) or (>= 0xAB30 and <= 0xAB6F);

    /// <summary>Step 6: the letters with no decomposition that are folded all the same.</summary>
    private static int AppendFoldedLetter(Rune folded, Span<char> output)
    {
        string? replacement = folded.Value switch
        {
            'ø' => "o",
            'æ' => "ae",
            'œ' => "oe",
            'ß' => "ss",
            'ł' => "l",
            'đ' => "d",
            'ð' => "d",
            'þ' => "th",
            'ħ' => "h",
            _ => null,
        };

        if (replacement is null) return folded.EncodeToUtf16(output);

        replacement.CopyTo(output);
        return replacement.Length;
    }

    /// <summary>Lowercases ASCII text, allocating only if there is anything to lowercase.</summary>
    private static string LowercaseAscii(string text)
    {
        if (!text.AsSpan().ContainsAnyInRange('A', 'Z')) return text;

        return string.Create(text.Length, text, static (lowered, original) => Ascii.ToLower(original, lowered, out _));
    }
}
