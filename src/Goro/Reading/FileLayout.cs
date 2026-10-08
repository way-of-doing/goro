using System.Collections.Immutable;
using Goro.Reading.Bytes;
using Goro.Reading.Tags;

namespace Goro.Reading;

public enum AudioFormat
{
    Mp3,
}

public enum AudioNotFoundReason
{
    /// <summary>The search covered everything between the tags, and found no frame.</summary>
    NoFrame,

    /// <summary>The search reached its limit before it reached the trailing tags.</summary>
    SearchGaveUp,
}

/// <summary>Where a file's audio is, as far as the edges show it; the positions are those of docs/design/mp3-layout.md.</summary>
public abstract record AudioLocation
{
    private AudioLocation()
    {
    }

    /// <param name="FirstFrame"><c>C</c>: the first confirmed frame.</param>
    /// <param name="TrailingTagsStart"><c>F</c>: where the trailing tags start, or the end of the file.</param>
    public sealed record Found(long FirstFrame, long TrailingTagsStart) : AudioLocation;

    /// <summary>No audio could be found: the file cannot be read (docs/concepts/warnings.md).</summary>
    public sealed record NotFound(AudioNotFoundReason Reason) : AudioLocation;
}

/// <summary>
/// What the analysis of a file's edges found: its tags, with an index of their fields, where its
/// audio is, and every condition met on the way. It describes a file however damaged rather than
/// failing on it; what the description means is for each command to decide.
/// </summary>
public sealed record FileLayout(
    AudioFormat Format,
    ImmutableArray<TagIndex> Tags,
    AudioLocation Audio,
    ImmutableArray<Condition> Conditions,
    ReadLog Reads)
{
    /// <summary>Part of the file could not be read: what the <c>incomplete</c> warning counts.</summary>
    public bool IsIncomplete => Conditions.Any(condition => condition.MakesIncomplete);

    /// <summary>The tag a source of <paramref name="format"/> reads, if the file has one.</summary>
    public TagIndex? Source(TagFormat format) =>
        Tags.FirstOrDefault(tag => tag.Kind.Format == format && tag.IsSource);
}
