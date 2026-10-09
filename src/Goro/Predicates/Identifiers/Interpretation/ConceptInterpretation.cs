using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers.Interpretation;

/// <summary>
/// How a concept turns what a field records into occurrences: see "What the datum yields" and
/// "Nothing left to yield" in docs/features/builtins/identifiers.md. Every concept trims a value
/// before using it. A string concept drops one left empty; a concept of any other type makes it an
/// unusable occurrence.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is how a value recorded as text is read, by every source unless a source says
/// otherwise: <see cref="Id3v2"/> for a convention of Id3v2's own, <see cref="Id3v1Byte"/> for an
/// Id3v1 field recorded as a single byte rather than as text.
/// </remarks>
public sealed class ConceptInterpretation<T> where T : notnull
{
    public required Func<string, Origin, IEnumerable<Occurrence<T>>> Text { get; init; }

    public Func<string, Origin, IEnumerable<Occurrence<T>>>? Id3v2 { get; init; }

    /// <summary>An Id3v1 byte field: no occurrence where the byte is the convention for "nothing recorded".</summary>
    public Func<byte, Origin, Occurrence<T>?>? Id3v1Byte { get; init; }
}

/// <summary>The interpretations of the concepts of docs/features/builtins/identifiers.md.</summary>
public static class ConceptInterpretations
{
    /// <summary>Text, trimmed; nothing at all where only whitespace was recorded.</summary>
    public static ConceptInterpretation<string> Strings { get; } = new()
    {
        Text = (value, _) => Trimmed(value) is { } text ? [new Usable<string>(text)] : [],
    };

    /// <summary>
    /// A genre: plain text in APE, which applies no conventions; Id3v2's <c>TCON</c> conventions in
    /// Id3v2; and an index into the genre table in Id3v1.
    /// </summary>
    public static ConceptInterpretation<string> Genres { get; } = new()
    {
        Text = Strings.Text,
        Id3v2 = (value, _) => Interpretation.Genres.ResolveTcon(value).Select(Occurrence<string> (genre) => new Usable<string>(genre)),
        Id3v1Byte = (value, origin) => value == Interpretation.Genres.Id3v1NoGenre
            ? null
            : Interpretation.Genres.Name(value) is { } name ? new Usable<string>(name) : new Unusable<string>(origin),
    };

    /// <summary>A track number, from text, or from Id3v1.1's track byte, where zero is "not recorded".</summary>
    public static ConceptInterpretation<decimal> Tracks { get; } = new()
    {
        Text = (value, origin) => [Number(TrackNumbers.Parse(value.Trim()), origin)],
        Id3v1Byte = (value, _) => value == 0 ? null : new Usable<decimal>(value),
    };

    /// <summary>A year, from date-shaped text.</summary>
    public static ConceptInterpretation<decimal> Years { get; } = new()
    {
        Text = (value, origin) => [Number(DateShapes.Year(value.Trim()), origin)],
    };

    private static string? Trimmed(string value) => value.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    private static Occurrence<decimal> Number(decimal? number, Origin origin) =>
        number is { } value ? new Usable<decimal>(value) : new Unusable<decimal>(origin);
}
