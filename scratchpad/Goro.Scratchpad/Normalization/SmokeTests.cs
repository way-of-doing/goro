namespace Goro.Scratchpad.Normalization;

// The smoke cases recorded in docs/testing.md under "String normalization", run against any
// number of normalizers side by side. Expectations are those of docs/concepts/normalization.md.
public static class SmokeTests
{
    enum Kind { Equal, NotEqual, LiteralEqual, LiteralNotEqual, Matches, NoMatch, LiteralMatches, Less, NoThrow }

    sealed record Case(string Name, Kind Kind, string A, string B = "");

    static readonly Case[] Cases =
    [
        new("case and diacritic folding",              Kind.Equal,           "Motörhead", "motorhead"),
        new("plain case folding",                      Kind.Equal,           "METALLICA", "metallica"),
        new("composed and decomposed are equal",       Kind.Equal,           "Motörhead", "Motörhead"),
        new("...also under LITERALLY",                 Kind.LiteralEqual,    "Motörhead", "Motörhead"),
        new("LITERALLY keeps diacritics",              Kind.LiteralNotEqual, "Motörhead", "Motorhead"),
        new("LITERALLY keeps case",                    Kind.LiteralNotEqual, "Metallica", "metallica"),
        new("Turkish dotted capital I",                Kind.Equal,           "İstanbul", "istanbul"),
        new("Greek accents fold",                      Kind.Equal,           "Βαγγέλης", "βαγγελης"),
        new("Greek final sigma",                       Kind.Equal,           "ΟΔΥΣΣΕΥΣ", "οδυσσευς"),
        new("Cyrillic yo folds to ye",                 Kind.Equal,           "Ёлка", "елка"),
        new("letter fold: o-slash",                    Kind.Equal,           "Ørsted", "orsted"),
        new("letter fold: sharp s",                    Kind.Equal,           "Straße", "strasse"),
        new("letter fold: ae ligature",                Kind.Equal,           "Ænima", "aenima"),
        new("letter fold: Polish l-stroke",            Kind.Equal,           "Łódź", "lodz"),
        new("letter fold: Vietnamese d-stroke",        Kind.Equal,           "Đà Nẵng", "da nang"),
        new("letter fold: Icelandic thorn",            Kind.Equal,           "Þursaflokkurinn", "thursaflokkurinn"),
        new("Cyrillic short i is a letter",            Kind.NotEqual,        "Йога", "иога"),
        new("Ukrainian yi is a letter",                Kind.NotEqual,        "Київ", "Киів"),
        new("Vietnamese stacked diacritics",           Kind.Equal,           "Đội", "doi"),
        new("soft hyphen is ignored",                  Kind.Equal,           "Radio­head", "radiohead"),
        new("variation selector is ignored",           Kind.Equal,           "❤️", "❤"),
        new("Hebrew points are kept",                  Kind.NotEqual,        "שָׁלוֹם", "שלום"),
        new("kana voicing is not a diacritic",         Kind.NotEqual,        "ガンダム", "カンタム"),
        new("Devanagari vowel signs are kept",         Kind.NotEqual,        "कुमार", "कमार"),
        new("Thai tone marks are kept",                Kind.NotEqual,        "ไม้", "ไม"),
        new("Hangul compares equal to itself",         Kind.Equal,           "방탄소년단", "방탄소년단"),
        new("fullwidth folds to ASCII",                Kind.Equal,           "ＹＭＯ", "ymo"),
        new("halfwidth katakana folds",                Kind.Equal,           "ｶﾞﾝﾀﾞﾑ", "ガンダム"),
        new("ligature fi folds",                       Kind.Equal,           "ﬁre", "fire"),
        new("regex: pattern without diacritic",        Kind.Matches,         "Motörhead", "^mot"),
        new("regex: pattern with diacritic",           Kind.NoMatch,         "Motörhead", "motö"),
        new("regex: uppercase pattern",                Kind.Matches,         "Motörhead", "^MOT"),
        new("regex: Hangul pattern",                   Kind.Matches,         "방탄소년단", "소년"),
        new("regex: kana pattern keeps voicing",       Kind.Matches,         "ガンダム", "ガン"),
        new("regex: sharp s folded in subject",        Kind.Matches,         "Straße", "strasse"),
        new("regex: LITERALLY keeps diacritic",        Kind.LiteralMatches,  "Motörhead", "otö"),
        new("order: plain letters",                    Kind.Less,            "abc", "abd"),
        new("order: by code point, not UTF-16 unit",   Kind.Less,            "�", "\U0001F600"),
        new("lone surrogate does not throw",           Kind.NoThrow,         "abc\uD800def"),
    ];

    public static void Run(IReadOnlyList<INormalizer> normalizers)
    {
        Console.WriteLine($"{"smoke test",-42}" + string.Concat(normalizers.Select(n => $"{n.Name,-12}")));
        var fails = new int[normalizers.Count];
        foreach (var c in Cases)
        {
            var line = $"{c.Name,-42}";
            for (int i = 0; i < normalizers.Count; i++)
            {
                string result;
                try { result = Check(normalizers[i], c) ? "ok" : "FAIL"; }
                catch (Exception) { result = "THROWS"; }
                if (result != "ok") fails[i]++;
                line += $"{result,-12}";
            }
            Console.WriteLine(line);
        }
        Console.WriteLine($"{"failures",-42}" + string.Concat(fails.Select(f => $"{f,-12}")));
        Console.WriteLine();
    }

    static bool Check(INormalizer n, Case c) => c.Kind switch
    {
        Kind.Equal           => n.Normalize(c.A) == n.Normalize(c.B),
        Kind.NotEqual        => n.Normalize(c.A) != n.Normalize(c.B),
        Kind.LiteralEqual    => n.Literal(c.A) == n.Literal(c.B),
        Kind.LiteralNotEqual => n.Literal(c.A) != n.Literal(c.B),
        Kind.Matches         => n.Match(c.A, c.B, literally: false),
        Kind.NoMatch         => !n.Match(c.A, c.B, literally: false),
        Kind.LiteralMatches  => n.Match(c.A, c.B, literally: true),
        Kind.Less            => n.Compare(n.Normalize(c.A), n.Normalize(c.B)) < 0,
        Kind.NoThrow         => n.Normalize(c.A) is not null && n.Literal(c.A) is not null,
        _ => throw new InvalidOperationException(),
    };
}
