using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Goro.Scratchpad.Normalization;

// Candidate string normalizations for predicate comparisons; see docs/concepts/normalization.md.
public interface INormalizer
{
    string Name { get; }
    string Normalize(string s);            // default (normalized) comparison mode
    string Literal(string s);              // LITERALLY() mode
    int Compare(string a, string b);       // ordering of two already-prepared strings
    bool Match(string subject, string pattern, bool literally);
}

// What docs/concepts/predicates.md said before normalization.md existed: NFD, drop every combining
// mark, lowercase. Kept as the baseline the others are measured against.
public sealed class ReferenceNormalizer : INormalizer
{
    public string Name => "reference";

    public string Normalize(string s)
    {
        if (System.Text.Ascii.IsValid(s)) return AsciiText.Lower(s);   // shared fast path; same result for ASCII
        var d = s.Normalize(NormalizationForm.FormD);                // throws on ill-formed UTF-16
        var sb = new StringBuilder(d.Length);
        foreach (var r in d.EnumerateRunes())
            if (!IsMark(r)) sb.Append(r);
        return sb.ToString().ToLowerInvariant();
    }

    public string Literal(string s) => s;

    public int Compare(string a, string b) => string.CompareOrdinal(a, b);

    public bool Match(string subject, string pattern, bool literally)
    {
        string subj = literally ? subject : StripMarks(subject);       // diacritics only; case from the engine
        return new Regex(pattern, RegexOpts.For(literally)).IsMatch(subj);
    }

    static string StripMarks(string s)
    {
        if (System.Text.Ascii.IsValid(s)) return s;
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var r in d.EnumerateRunes()) if (!IsMark(r)) sb.Append(r);
        return sb.ToString();
    }

    static bool IsMark(Rune r) => Rune.GetUnicodeCategory(r) is
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
}

// What docs/concepts/normalization.md specifies (as of commit f2a980b).
public sealed class ProposedNormalizer : INormalizer
{
    public string Name => "proposed";

    // Letters with no decomposition that are folded anyway (applied after case folding).
    static readonly Dictionary<Rune, string> LetterFolds = new()
    {
        [new Rune('ø')] = "o", [new Rune('æ')] = "ae", [new Rune('œ')] = "oe", [new Rune('ß')] = "ss",
        [new Rune('ł')] = "l", [new Rune('đ')] = "d", [new Rune('ð')] = "d", [new Rune('þ')] = "th",
        [new Rune('ħ')] = "h",
    };

    // Per-BMP-character data, precomputed once: the case-folded character ('\0' marks a letter fold)
    // and a flags byte.
    const byte Mark = 1, Ignorable = 2, LatinGreek = 4;
    static readonly char[] FoldBmp = new char[0x10000];
    static readonly byte[] FlagsBmp = new byte[0x10000];

    static ProposedNormalizer()
    {
        for (int c = 0; c < 0x10000; c++)
        {
            if (c is >= 0xD800 and <= 0xDFFF) { FoldBmp[c] = (char)c; continue; }
            var r = new Rune(c);
            var f = Rune.ToLowerInvariant(Rune.ToUpperInvariant(r));
            FoldBmp[c] = LetterFolds.ContainsKey(f) ? '\0' : f.IsBmp ? (char)f.Value : (char)c;
            FlagsBmp[c] = Flags(r);
        }
    }

    static byte Flags(Rune r)
    {
        byte b = 0;
        if (Rune.GetUnicodeCategory(r) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark) b |= Mark;
        if (IsDefaultIgnorable(r.Value)) b |= Ignorable;
        if (IsLatinOrGreek(r.Value)) b |= LatinGreek;
        return b;
    }

    // Default_Ignorable_Code_Point (DerivedCoreProperties.txt), as ranges.
    static bool IsDefaultIgnorable(int v) => v is 0x00AD or 0x034F or 0x061C or (>= 0x115F and <= 0x1160)
        or (>= 0x17B4 and <= 0x17B5) or (>= 0x180B and <= 0x180F) or (>= 0x200B and <= 0x200F)
        or (>= 0x202A and <= 0x202E) or (>= 0x2060 and <= 0x206F) or 0x3164 or (>= 0xFE00 and <= 0xFE0F)
        or 0xFEFF or 0xFFA0 or (>= 0xFFF0 and <= 0xFFF8) or (>= 0x1BCA0 and <= 0x1BCA3)
        or (>= 0x1D173 and <= 0x1D17A) or (>= 0xE0000 and <= 0xE0FFF);

    // Letters whose accents are stripped: the Latin and Greek blocks.
    static bool IsLatinOrGreek(int v) => v is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= 0x00C0 and <= 0x02AF)
        or (>= 0x0370 and <= 0x03FF) or (>= 0x1D00 and <= 0x1DBF) or (>= 0x1E00 and <= 0x1FFF)
        or (>= 0x2C60 and <= 0x2C7F) or (>= 0xA720 and <= 0xA7FF) or (>= 0xAB30 and <= 0xAB6F);

    public string Normalize(string s)
    {
        if (System.Text.Ascii.IsValid(s)) return AsciiText.Lower(s);          // fast path: most tags are ASCII
        var d = Sanitize(s).Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(d.Length);
        bool simple = true;                                                  // all below U+0300: already NFC
        bool latinGreekBase = false;
        char last = '\0';
        foreach (var r in d.EnumerateRunes())
        {
            byte fl = r.IsBmp ? FlagsBmp[r.Value] : Flags(r);
            if ((fl & Ignorable) != 0) continue;                                 // soft hyphen, ZWJ, variation selectors...
            if ((fl & Mark) != 0)
            {
                if (latinGreekBase) continue;                                    // accents on Latin and Greek letters
                if (r.Value == 0x0308 && last == 'е') continue;                  // Cyrillic ё folds to е; й, ї, ў do not
                sb.Append(r); simple = false; continue;                          // every other mark is kept
            }
            latinGreekBase = (fl & LatinGreek) != 0;
            if (r.IsBmp)
            {
                char f = FoldBmp[r.Value];
                if (f == '\0') { sb.Append(LetterFolds[Rune.ToLowerInvariant(Rune.ToUpperInvariant(r))]); last = '\0'; }
                else { sb.Append(f); simple &= f < 0x300; last = f; }
            }
            else { sb.Append(Rune.ToLowerInvariant(Rune.ToUpperInvariant(r))); simple = false; last = '\0'; }
        }
        var folded = sb.ToString();
        return simple ? folded : folded.Normalize(NormalizationForm.FormC);   // recompose: Hangul, kana, й, ...
    }

    public string Literal(string s)
    {
        foreach (char c in s) if (c >= 0x300) return Sanitize(s).Normalize(NormalizationForm.FormC);
        return s;                                                            // all below U+0300: already NFC
    }

    public int Compare(string a, string b)                                   // code point order, not UTF-16 order
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            char x = a[i], y = b[i];
            if (x != y) return Fix(x) - Fix(y);
        }
        return a.Length - b.Length;
        static int Fix(char c) => c >= 0xE000 ? c - 0x800 : c >= 0xD800 ? c + 0x2000 : c;
    }

    public bool Match(string subject, string pattern, bool literally)
    {
        // The subject is prepared exactly as for every other operator; the pattern is untouched.
        string subj = literally ? Literal(subject) : Normalize(subject);
        return new Regex(pattern, RegexOpts.For(literally)).IsMatch(subj);
    }

    // Replace unpaired surrogates with U+FFFD so that normalization is total. Allocates only if needed.
    static string Sanitize(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (!char.IsSurrogate(c)) continue;
            if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { i++; continue; }
            var sb = new StringBuilder(s.Length);
            foreach (var r in s.EnumerateRunes()) sb.Append(r);               // EnumerateRunes yields U+FFFD for ill-formed
            return sb.ToString();
        }
        return s;
    }
}

// Prior art in .NET itself: ICU collation via CompareInfo, at "ignore case, accents and width"
// strength. Normalize returns the sort key, so equality and order of keys are the collator's.
// Regex cannot be expressed this way; Match falls back to an unprepared, case-insensitive match.
public sealed class CollationNormalizer : INormalizer
{
    static readonly CompareInfo Ci = CultureInfo.InvariantCulture.CompareInfo;
    const CompareOptions Opts = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth;

    public string Name => "collation";
    public string Normalize(string s) => Key(Ci.GetSortKey(s, Opts).KeyData);
    public string Literal(string s) => Key(Ci.GetSortKey(s, CompareOptions.None).KeyData);
    public int Compare(string a, string b) => string.CompareOrdinal(a, b);   // keys compare bytewise
    public bool Match(string subject, string pattern, bool literally) =>
        new Regex(pattern, RegexOpts.For(literally)).IsMatch(subject);

    static string Key(byte[] b) =>
        string.Create(b.Length, b, static (d, k) => { for (int i = 0; i < k.Length; i++) d[i] = (char)k[i]; });
}

static class RegexOpts
{
    public static RegexOptions For(bool literally) =>
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (literally ? 0 : RegexOptions.IgnoreCase);
}

static class AsciiText
{
    // Lowercases without allocating when there is nothing to lowercase.
    public static string Lower(string s)
    {
        foreach (char c in s)
            if (c is >= 'A' and <= 'Z')
                return string.Create(s.Length, s, static (dst, src) => System.Text.Ascii.ToLower(src, dst, out _));
        return s;
    }
}
