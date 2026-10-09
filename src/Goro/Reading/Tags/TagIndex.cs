using System.Collections.Immutable;

namespace Goro.Reading.Tags;

public enum TagFormat
{
    Id3v2,
    Ape,
    Id3v1,
    Lyrics3,
}

/// <summary>
/// A tag's format and revision: 2, 3 or 4 for Id3v2; 1 or 2 for APE; 0 or 1 for Id3v1; 2 for
/// Lyrics3. A tag whose revision could not be read, an Id3v2 tag claiming v2.5 say, keeps the number
/// it claimed.
/// </summary>
public readonly record struct TagKind(TagFormat Format, int Revision)
{
    public override string ToString() => Format switch
    {
        TagFormat.Id3v2 => $"id3v2.{Revision}",
        TagFormat.Ape => $"ape{Revision}",
        TagFormat.Id3v1 => $"id3v1.{Revision}",
        TagFormat.Lyrics3 => $"lyrics3v{Revision}",
        _ => $"{Format}{Revision}",
    };
}

/// <summary>How much of a tag could be read; see "Damaged data" in docs/features/builtins/identifiers.md.</summary>
public enum TagState
{
    /// <summary>Its whole structure was read.</summary>
    Intact,

    /// <summary>Its structure broke off part way: what lies before the break is indexed, and nothing after it.</summary>
    BrokenOff,

    /// <summary>Nothing in it could be read: its source is absent everywhere.</summary>
    Unusable,
}

/// <summary>
/// One tag found in a file: where it lies, how much of it could be read, and an index of its fields.
/// Only <see cref="IsSource"/> tags are what a source reads; another tag of the same format, a second
/// Id3v2 tag say, was read for its extent and damage only.
/// </summary>
public sealed record TagIndex(TagKind Kind, Region Region, TagState State, ImmutableArray<FieldEntry> Fields, bool IsSource = true)
{
    public static TagIndex Unusable(TagKind kind, Region region) => new(kind, region, TagState.Unusable, []);
}
