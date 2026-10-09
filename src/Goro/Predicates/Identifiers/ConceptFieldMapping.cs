using Goro.Reading.Tags;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The field each tag source's cell of a concept reads, by the name the source gives it: the cells
/// of the table of concepts in docs/features/builtins/identifiers.md, one class for each source.
/// </summary>
public static class ConceptFieldMapping
{
    /// <summary>Vorbis comment field names, by convention.</summary>
    public static class Vorbis
    {
        public const string Artist = "ARTIST";
        public const string Album = "ALBUM";
        public const string Genre = "GENRE";
        public const string Title = "TITLE";
        public const string Track = "TRACKNUMBER";
        public const string Year = "DATE";
    }

    /// <summary>APE item keys, by convention: APE prescribes none.</summary>
    public static class Ape
    {
        public const string Artist = "Artist";
        public const string Album = "Album";
        public const string Genre = "Genre";
        public const string Title = "Title";
        public const string Track = "Track";
        public const string Year = "Year";
    }

    /// <summary>Id3v2 frames, by the v2.4 identifier Goro reads them under.</summary>
    public static class Id3v2
    {
        public const string Artist = "TPE1";
        public const string Album = "TALB";
        public const string Genre = "TCON";
        public const string Title = "TIT2";
        public const string Track = "TRCK";
        public const string Year = "TDRC";
    }

    /// <summary>Id3v1's fixed fields.</summary>
    public static class Id3v1
    {
        public const string Artist = Id3v1Fields.Artist;
        public const string Album = Id3v1Fields.Album;
        public const string Genre = Id3v1Fields.Genre;
        public const string Title = Id3v1Fields.Title;
        public const string Track = Id3v1Fields.Track;
        public const string Year = Id3v1Fields.Year;
    }
}
