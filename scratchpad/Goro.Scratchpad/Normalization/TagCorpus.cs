namespace Goro.Scratchpad.Normalization;

// Synthetic tag-like strings ("<name> <ASCII word> <number>") for benchmarking normalization.
public sealed class TagCorpus
{
    static readonly string[] Ascii =
    [
        "Metallica", "The Beatles", "Pink Floyd", "Daft Punk", "Massive Attack", "Boards of Canada", "Radiohead",
        "Nine Inch Nails", "Rock", "Post-Rock", "Live at Wembley", "Remastered", "Disc 1", "Greatest Hits",
        "Various Artists", "Original Motion Picture Soundtrack", "feat. Someone", "Bonus Track", "Demo",
    ];
    static readonly string[] Latin =
    [
        "Motörhead", "Björk", "Sigur Rós", "Mötley Crüe", "Céline Dion", "Françoise Hardy", "Ørsted", "Straße",
        "Édith Piaf", "Antônio Carlos Jobim", "Dvořák", "Łódź", "Đà Nẵng", "Señor", "Café del Mar", "Ænima",
    ];
    static readonly string[] Other =
    [
        "ガンダム", "宇多田ヒカル", "椎名林檎", "きゃりーぱみゅぱみゅ", "ＹＭＯ", "ｶﾞﾝﾀﾞﾑ", "방탄소년단", "아이유",
        "Кино", "Ёлка", "Земфира", "Βαγγέλης Παπαθανασίου", "ΟΔΥΣΣΕΥΣ", "लता मंगेशकर", "คาราบาว",
    ];

    public required string[] Mixed { get; init; }      // 75% ASCII, 15% accented Latin, 10% other scripts
    public required string[] AsciiOnly { get; init; }
    public required string[] NonAscii { get; init; }   // half accented Latin, half other scripts

    public static TagCorpus Create(int size = 200_000, int seed = 42)
    {
        var rnd = new Random(seed);
        string Pick(string[] words) => words[rnd.Next(words.Length)];
        string Make(Func<string> head) => $"{head()} {Pick(Ascii)} {rnd.Next(1, 100)}";

        return new TagCorpus
        {
            Mixed = Enumerable.Range(0, size).Select(_ => rnd.Next(100) switch
            {
                < 75 => Make(() => Pick(Ascii)),
                < 90 => Make(() => Pick(Latin)),
                _ => Make(() => Pick(Other)),
            }).ToArray(),
            AsciiOnly = Enumerable.Range(0, size).Select(_ => Make(() => Pick(Ascii))).ToArray(),
            NonAscii = Enumerable.Range(0, size).Select(_ => Make(() => rnd.Next(2) == 0 ? Pick(Latin) : Pick(Other))).ToArray(),
        };
    }
}
