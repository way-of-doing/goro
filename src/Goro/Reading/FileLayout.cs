using System.Collections.Immutable;
using Goro.Reading.Bytes;
using Goro.Reading.Flac;
using Goro.Reading.Mp3;
using Goro.Reading.Tags;

namespace Goro.Reading;

public enum AudioFormat
{
    Mp3,
    Flac,
}

public enum AudioNotFoundReason
{
    /// <summary>The search covered everything between the tags, and found no frame, or no <c>fLaC</c> marker.</summary>
    NoFrame,

    /// <summary>The search reached its limit before it reached the trailing tags.</summary>
    SearchGaveUp,
}

/// <summary>Where a file's audio is, as far as the edges show it; the positions are those of docs/design/mp3-layout.md and flac-layout.md.</summary>
public abstract record AudioLocation
{
    private AudioLocation()
    {
    }

    /// <param name="FirstFrame">The first confirmed frame: <c>C</c> for an MP3, <c>D</c> for a FLAC file.</param>
    /// <param name="TrailingTagsStart"><c>F</c>: where the trailing tags start, or the end of the file.</param>
    public sealed record Found(long FirstFrame, long TrailingTagsStart) : AudioLocation;

    /// <summary>No audio could be found: the file cannot be read (docs/concepts/warnings.md).</summary>
    public sealed record NotFound(AudioNotFoundReason Reason) : AudioLocation;
}

/// <summary>
/// What the analysis of a file's edges found: its tags, with an index of their fields, where its
/// audio is, its playing time, and every condition met on the way. It describes a file however
/// damaged rather than failing on it; what the description means is for each command to decide.
/// </summary>
/// <param name="Details">What the analysis learnt that belongs to the format alone.</param>
public sealed record FileLayout(
    AudioFormat Format,
    ImmutableArray<TagIndex> Tags,
    AudioLocation Audio,
    DurationOutcome Duration,
    FormatDetails Details,
    ImmutableArray<Condition> Conditions,
    ReadLog Reads)
{
    /// <summary>An MP3's summary frame ahead of the audio, if there is one.</summary>
    public SummaryFrame? Summary => (Details as FormatDetails.Mp3)?.Summary;

    /// <summary>Part of the file could not be read: what the <c>incomplete</c> warning counts.</summary>
    public bool IsIncomplete => Conditions.Any(condition => condition.MakesIncomplete);

    /// <summary>The tag a source of <paramref name="format"/> reads, if the file has one.</summary>
    public TagIndex? Source(TagFormat format) =>
        Tags.FirstOrDefault(tag => tag.Kind.Format == format && tag.IsSource);
}

/// <summary>What an analysis learnt that belongs to one format alone.</summary>
public abstract record FormatDetails
{
    private FormatDetails()
    {
    }

    /// <param name="Summary">The summary frame ahead of the audio (Xing, Info or VBRI), if there is one.</param>
    public sealed record Mp3(SummaryFrame? Summary) : FormatDetails;

    /// <param name="Marker"><c>C</c>: where <c>fLaC</c> is, if it was found.</param>
    /// <param name="Metadata">The block chain, if the marker was found.</param>
    public sealed record Flac(long? Marker, FlacMetadata? Metadata) : FormatDetails
    {
        public StreamInfo? StreamInfo => Metadata?.StreamInfo;
    }
}
