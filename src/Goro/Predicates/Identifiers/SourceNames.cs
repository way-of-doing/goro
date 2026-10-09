namespace Goro.Predicates.Identifiers;

/// <summary>
/// The names of the sources a predicate can qualify an identifier by, as docs/features/builtins/identifiers.md
/// writes them. A predicate may write them in any case.
/// </summary>
public static class SourceNames
{
    public const string File = "file";
    public const string Vorbis = "vorbis";
    public const string Ape = "ape";
    public const string Id3v2 = "id3v2";
    public const string Id3v1 = "id3v1";
}
