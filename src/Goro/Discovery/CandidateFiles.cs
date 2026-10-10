using Goro.Reading;

namespace Goro.Discovery;

/// <summary>
/// Decides which files Goro considers at all: those of a format it can hash reliably, judged by
/// extension alone so that discovery never has to open a file. See the CANDIDATES section of
/// docs/concepts/pathspecs.md.
/// </summary>
public static class CandidateFiles
{
    /// <summary>
    /// The extension of a file name as <c>file::extension</c> defines it: the part after the last
    /// dot, or null when there is no dot, when the only dot is the first character, or when
    /// nothing follows the last dot. Unlike <see cref="Path.GetExtension(string?)"/>, a name such
    /// as <c>.mp3</c> has no extension.
    /// </summary>
    public static string? Extension(string fileName)
    {
        var lastDot = fileName.LastIndexOf('.');
        return lastDot > 0 && lastDot < fileName.Length - 1 ? fileName[(lastDot + 1)..] : null;
    }

    /// <summary>
    /// The format a file is taken to hold, by its extension; null for a file Goro does not consider.
    /// A format is admitted here, through <see cref="AudioFormats"/>, only once its audio hash
    /// survives tag edits: Ogg has a line of development in docs/directions/ that ends by adding it.
    /// </summary>
    public static AudioFormat? FormatOf(string filePath) =>
        Extension(Path.GetFileName(filePath)) is { } extension ? AudioFormats.FromExtension(extension) : null;

    public static bool IsCandidate(string filePath) => FormatOf(filePath) is not null;
}
