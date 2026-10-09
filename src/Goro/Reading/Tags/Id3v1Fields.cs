namespace Goro.Reading.Tags;

/// <summary>
/// The names <see cref="Id3v1Index"/> gives the fixed fields of an Id3v1 tag, which has none of its
/// own: its fields are known by their place.
/// </summary>
public static class Id3v1Fields
{
    public const string Title = "title";
    public const string Artist = "artist";
    public const string Album = "album";
    public const string Year = "year";
    public const string Comment = "comment";
    public const string Track = "track";
    public const string Genre = "genre";
}
