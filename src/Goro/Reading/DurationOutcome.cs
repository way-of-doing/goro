namespace Goro.Reading;

/// <summary>What a playing time rests on, when it can be had.</summary>
public enum DurationBasis
{
    /// <summary>A summary header whose byte count agrees with the audio.</summary>
    SummaryHeader,

    /// <summary>A summary header trusted up to an exact frame end, the bytes after it being junk (docs/implementation.md).</summary>
    SummaryHeaderToFrameEnd,

    /// <summary>No summary header, and a constant bitrate shown from the edges (docs/implementation.md).</summary>
    ConstantBitrateFromEdges,
}

/// <summary>Why a playing time cannot be had.</summary>
public enum DurationProblem
{
    /// <summary>No audio was found, so the file cannot be read at all.</summary>
    NoAudio,

    /// <summary>No summary header, and a constant bitrate could not be shown from the edges.</summary>
    NotConstantBitrate,

    /// <summary>A free-format stream without a summary header: no bitrate to count frames by.</summary>
    FreeFormatWithoutHeader,

    /// <summary>A summary header that records no byte count, which leaves nothing to check its frame count against.</summary>
    NoByteCount,

    /// <summary>A summary header whose LAME tag fails its CRC.</summary>
    SummaryCrcFails,

    /// <summary>A summary header describing more audio than the file holds, or less, with no frame ending where it says.</summary>
    SummaryDisagrees,

    /// <summary>Gapless values claiming more samples than the stream holds.</summary>
    GaplessExceedsStream,
}

/// <summary>A file's playing time: the length of the stream's timeline, or why it cannot be had.</summary>
public abstract record DurationOutcome
{
    private DurationOutcome()
    {
    }

    /// <param name="Samples">The samples of the timeline, the encoder's delay and padding removed where the file records them.</param>
    public sealed record Known(long Samples, int SampleRate, DurationBasis Basis) : DurationOutcome
    {
        /// <summary>The playing time in whole seconds, truncated.</summary>
        public long WholeSeconds => Samples / SampleRate;
    }

    public sealed record Unusable(DurationProblem Problem) : DurationOutcome;
}
