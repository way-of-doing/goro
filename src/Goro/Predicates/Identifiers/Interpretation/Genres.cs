using System.Collections.Immutable;

namespace Goro.Predicates.Identifiers.Interpretation;

/// <summary>
/// The genre table, and how a genre recorded in Id3v2's <c>TCON</c> or Id3v1's genre byte resolves
/// to names: see "Genres" and "Genre" in docs/features/builtins/identifiers.md, and the table in
/// docs/features/builtins/genres.md.
/// </summary>
public static class Genres
{
    /// <summary>
    /// The 148 entries: Id3v1's original 80 and the extensions Winamp added, under the spellings
    /// TagLib gives them rather than the historical ones (docs/design/quirks.md).
    /// </summary>
    public static ImmutableArray<string> Table { get; } =
    [
        "Blues", "Classic Rock", "Country", "Dance", "Disco", "Funk", "Grunge", "Hip-Hop", "Jazz", "Metal",
        "New Age", "Oldies", "Other", "Pop", "R&B", "Rap", "Reggae", "Rock", "Techno", "Industrial",
        "Alternative", "Ska", "Death Metal", "Pranks", "Soundtrack", "Euro-Techno", "Ambient", "Trip-Hop", "Vocal", "Jazz-Funk",
        "Fusion", "Trance", "Classical", "Instrumental", "Acid", "House", "Game", "Sound Clip", "Gospel", "Noise",
        "Alternative Rock", "Bass", "Soul", "Punk", "Space", "Meditative", "Instrumental Pop", "Instrumental Rock", "Ethnic", "Gothic",
        "Darkwave", "Techno-Industrial", "Electronic", "Pop-Folk", "Eurodance", "Dream", "Southern Rock", "Comedy", "Cult", "Gangsta",
        "Top 40", "Christian Rap", "Pop/Funk", "Jungle", "Native American", "Cabaret", "New Wave", "Psychedelic", "Rave", "Showtunes",
        "Trailer", "Lo-Fi", "Tribal", "Acid Punk", "Acid Jazz", "Polka", "Retro", "Musical", "Rock & Roll", "Hard Rock",
        "Folk", "Folk Rock", "National Folk", "Swing", "Fast Fusion", "Bebop", "Latin", "Revival", "Celtic", "Bluegrass",
        "Avant-garde", "Gothic Rock", "Progressive Rock", "Psychedelic Rock", "Symphonic Rock", "Slow Rock", "Big Band", "Chorus", "Easy Listening", "Acoustic",
        "Humour", "Speech", "Chanson", "Opera", "Chamber Music", "Sonata", "Symphony", "Booty Bass", "Primus", "Porn Groove",
        "Satire", "Slow Jam", "Club", "Tango", "Samba", "Folklore", "Ballad", "Power Ballad", "Rhythmic Soul", "Freestyle",
        "Duet", "Punk Rock", "Drum Solo", "A Cappella", "Euro-House", "Dancehall", "Goa", "Drum & Bass", "Club-House", "Hardcore Techno",
        "Terror", "Indie", "Britpop", "Worldbeat", "Polsk Punk", "Beat", "Christian Gangsta Rap", "Heavy Metal", "Black Metal", "Crossover",
        "Contemporary Christian", "Christian Rock", "Merengue", "Salsa", "Thrash Metal", "Anime", "Jpop", "Synthpop",
    ];

    /// <summary>
    /// The historical spellings of the entries whose name differs from it by more than letter case:
    /// what a tagger that wrote a name after a reference, as in <c>(67)Psychadelic</c>, is likely to
    /// have written. They are used only to recognise such a repeat, and are never a genre's name.
    /// </summary>
    private static readonly ImmutableDictionary<int, string> HistoricalSpellings = new Dictionary<int, string>
    {
        [29] = "Jazz+Funk", [40] = "AlternRock", [67] = "Psychadelic", [81] = "Folk-Rock", [85] = "Bebob",
        [90] = "Avantgarde", [125] = "Dance Hall", [129] = "Hardcore", [133] = "Negerpunk",
    }.ToImmutableDictionary();

    /// <summary>The Id3v1 genre byte that is the convention for "no genre recorded".</summary>
    public const byte Id3v1NoGenre = 255;

    /// <summary>The name of a table position, or null where the table assigns none.</summary>
    public static string? Name(int index) => index >= 0 && index < Table.Length ? Table[index] : null;

    /// <summary>
    /// The genres one value of a <c>TCON</c> frame holds, by the conventions every revision has
    /// accumulated, in the order identifiers.md gives them. Each is trimmed, and one that is empty once
    /// trimmed is dropped, so a value can hold no genre at all.
    /// </summary>
    public static ImmutableArray<string> ResolveTcon(string value)
    {
        var genres = ImmutableArray.CreateBuilder<string>();
        foreach (var part in value.Split('/', ';'))
        {
            Resolve(part.Trim(), genres);
        }

        return genres.ToImmutable();
    }

    private static void Resolve(string part, ImmutableArray<string>.Builder genres)
    {
        // References in parentheses, as many as there are, each a genre of its own.
        var rest = part.AsSpan();
        Reference? last = null;
        while (rest.Length > 1 && rest[0] == '(' && rest[1] != '(' && rest.IndexOf(')') is var close and > 0)
        {
            if (ReferenceTo(rest[1..close]) is not { } reference)
            {
                break;
            }

            // A position the table assigns nothing to is left as it was recorded.
            Add(genres, reference.Name ?? rest[..(close + 1)].ToString());
            last = reference;
            rest = rest[(close + 1)..];
        }

        // What follows is text: a refinement of the reference before it, or the genre itself. A
        // doubled opening parenthesis is an escape for a literal one.
        var text = rest.StartsWith("((") ? rest[1..].ToString() : rest.ToString();
        if (last is { } previous)
        {
            if (!previous.IsRepeatedBy(text.Trim()))
            {
                Add(genres, text);
            }

            return;
        }

        // With no reference in parentheses, a bare number, RX or CR is one written without them.
        Add(genres, ReferenceTo(text)?.Name ?? text);
    }

    /// <summary>
    /// What a reference names: a table position by its number, or one of the reserved values RX and
    /// CR. <see cref="Name"/> is null for a position the table assigns no genre to.
    /// </summary>
    private readonly record struct Reference(string? Name, int? Index)
    {
        /// <summary>
        /// Whether a refinement only repeats this reference's name, as taggers wrote it so that readers
        /// not knowing the table would still show a name: under the name Goro gives the entry or its
        /// historical spelling, either without regard to case.
        /// </summary>
        public bool IsRepeatedBy(string refinement) =>
            string.Equals(refinement, Name, StringComparison.OrdinalIgnoreCase)
            || Index is { } index && HistoricalSpellings.TryGetValue(index, out var historical)
                && string.Equals(refinement, historical, StringComparison.OrdinalIgnoreCase);
    }

    private static Reference? ReferenceTo(ReadOnlySpan<char> reference)
    {
        if (reference is "RX")
        {
            return new Reference("Remix", null);
        }

        if (reference is "CR")
        {
            return new Reference("Cover", null);
        }

        if (reference.IsEmpty || !IsDigits(reference))
        {
            return null;
        }

        var index = int.TryParse(reference, System.Globalization.NumberStyles.None, null, out var parsed) ? parsed : -1;
        return new Reference(Name(index), index);
    }

    private static bool IsDigits(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static void Add(ImmutableArray<string>.Builder genres, string genre)
    {
        var trimmed = genre.Trim();
        if (trimmed.Length > 0)
        {
            genres.Add(trimmed);
        }
    }
}
