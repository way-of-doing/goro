using Goro.Messages;

namespace Goro.Discovery;

/// <summary>
/// A pathspec that cannot be resolved, or a glob that breaks the glob rules. Thrown only by
/// <see cref="IFileDiscoveryService.Resolve"/>, before any file is processed, so that the run can
/// be rejected with nothing done. See the FAILURES section of docs/concepts/pathspecs.md.
/// </summary>
/// <remarks>
/// <see cref="Error"/> is what is reported. <see cref="Exception.Message"/> holds its English
/// rendering only for debugging: the edge that reports the error renders it with its own provider.
/// </remarks>
public sealed class PathSpecException(string pathSpec, ErrorMessage error, Exception? innerException = null)
    : Exception(EnglishErrorMessages.Instance.Render(error), innerException)
{
    public string PathSpec { get; } = pathSpec;

    public ErrorMessage Error { get; } = error;
}
