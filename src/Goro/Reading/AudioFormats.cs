using Goro.Reading.Bytes;
using Goro.Reading.Flac;
using Goro.Reading.Mp3;

namespace Goro.Reading;

/// <summary>
/// Which format a file is taken to hold, by its extension (docs/concepts/pathspecs.md: a file is
/// judged by its name alone), and the analysis of each format's edges.
/// </summary>
public static class AudioFormats
{
    /// <summary>The format an extension names, compared without regard to case; null for one Goro does not read.</summary>
    public static AudioFormat? FromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "mp3" => AudioFormat.Mp3,
            "flac" => AudioFormat.Flac,
            _ => null,
        };

    /// <summary>Why a file of <paramref name="format"/> in which no audio can be found cannot be read, spelt whole for each format.</summary>
    public static string NoAudioFound(AudioFormat format) => format switch
    {
        AudioFormat.Mp3 => "no MPEG audio found",
        AudioFormat.Flac => "no FLAC audio found",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>The analysis of a file's edges, by the format it is taken to hold.</summary>
    public static FileLayout Analyse(AudioFormat format, BoundedReader reader) => format switch
    {
        AudioFormat.Mp3 => Mp3Analysis.Analyse(reader),
        AudioFormat.Flac => FlacAnalysis.Analyse(reader),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };
}
