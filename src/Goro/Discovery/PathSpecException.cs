namespace Goro.Discovery;

/// <summary>
/// A pathspec that cannot be resolved, or a glob that breaks the glob rules. Thrown only by
/// <see cref="IFileDiscoveryService.Resolve"/>, before any file is processed, so that the run can
/// be rejected with nothing done. See the FAILURES section of docs/concepts/pathspecs.md.
/// </summary>
public sealed class PathSpecException(string pathSpec, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string PathSpec { get; } = pathSpec;
}
