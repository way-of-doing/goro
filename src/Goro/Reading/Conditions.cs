using Goro.Reading.Tags;

namespace Goro.Reading;

/// <summary>
/// Something the analysis found about a file beyond its tags and audio: damage, a quirk, a gap. Each
/// is exposed whether or not anything uses it yet, for <c>goro audit</c> to report.
/// <see cref="MakesIncomplete"/> is what the <c>incomplete</c> warning counts
/// (docs/concepts/warnings.md).
/// </summary>
public abstract record Condition
{
    public virtual bool MakesIncomplete => false;
}

public enum TagUnusableReason
{
    /// <summary>An Id3v2 major version other than 2, 3 or 4.</summary>
    UnknownVersion,

    /// <summary>An Id3v2 tag size with a top bit set.</summary>
    SizeNotSyncsafe,

    /// <summary>A size that runs past the end of the file, or before where the tag could start.</summary>
    SizeOutsideFile,

    /// <summary>An APE header with no footer where its size says one is.</summary>
    NoFooter,

    /// <summary>A tag that must be resynchronised whole, and is more than the structure budget allows.</summary>
    TooLargeToResynchronise,

    /// <summary>An Id3v2.2 tag flagged as compressed, which that revision never defined.</summary>
    UndefinedCompression,
}

/// <summary>A tag of which nothing could be read: its source is absent everywhere.</summary>
public sealed record TagUnusable(TagKind Tag, Region Region, TagUnusableReason Reason) : Condition
{
    public override bool MakesIncomplete => true;
}

public enum StructureBreak
{
    /// <summary>An Id3v2 frame header without a legal identifier, which cannot be stepped over.</summary>
    NoFrameIdentifier,

    /// <summary>A frame or item whose size runs past the end of its tag.</summary>
    FieldRunsPastTag,

    /// <summary>Data after what read as the start of an Id3v2 tag's padding: a block of the tag was zeroed.</summary>
    DataAfterPadding,

    /// <summary>An Id3v2 extended header whose size runs past the tag.</summary>
    ExtendedHeaderRunsPastTag,

    /// <summary>An APE tag whose item count is more than its items.</summary>
    ItemCountTooHigh,

    /// <summary>An APE item whose key has no terminator inside the tag.</summary>
    ItemKeyUnterminated,

    /// <summary>The structure went on past what the budget for reading it allows.</summary>
    ReadLimitReached,
}

/// <summary>A tag whose structure broke off at <paramref name="At"/>: nothing after it was found.</summary>
public sealed record TagStructureBroken(TagKind Tag, long At, StructureBreak Reason) : Condition
{
    public override bool MakesIncomplete => true;
}

/// <summary>
/// The analysis needed more than its budget for <paramref name="Purpose"/> allows (see
/// <see cref="Bytes.ReadPolicy"/>), so something the file holds was not read.
/// </summary>
public sealed record ReadLimitReached(Bytes.ReadPurpose Purpose) : Condition
{
    public override bool MakesIncomplete => true;
}

/// <summary>A second tag of a format the file already has, which is not what its source reads.</summary>
public sealed record FurtherTag(TagKind Tag, Region Region) : Condition;

/// <summary>Bytes between where the leading tags end and the first audio frame.</summary>
public sealed record JunkBeforeAudio(Region Region) : Condition;

public enum TagQuirk
{
    /// <summary>Id3v2.4 frame sizes written as plain integers, as iTunes once did, and read as such.</summary>
    PlainFrameSizes,

    /// <summary>A frame with an illegal identifier, stepped over by its size (docs/design/quirks.md).</summary>
    IllegalFrameIdentifierSteppedOver,
}

/// <summary>A tag read as its writer meant rather than as its specification says.</summary>
public sealed record QuirkApplied(TagKind Tag, TagQuirk Quirk, long At) : Condition;

/// <summary>
/// Bytes after the audio a trusted summary header describes, and before the trailing tags: junk,
/// never part of the audio (docs/implementation.md).
/// </summary>
public sealed record BytesAfterAudio(Region Region) : Condition;

/// <summary>The file's playing time cannot be had: its <c>file::duration</c> is unusable, and the file is reported.</summary>
public sealed record DurationUnusable(DurationProblem Problem) : Condition
{
    public override bool MakesIncomplete => true;
}
