using Goro.Warnings;

namespace Goro.Discovery;

public interface IFileDiscoveryService
{
    /// <summary>
    /// Checks every pathspec before any file is discovered.
    /// </summary>
    /// <exception cref="PathSpecException">A pathspec cannot be resolved, or a glob breaks the glob rules.</exception>
    ResolvedPathSpecs Resolve(IReadOnlyList<string> pathSpecs);

    /// <summary>
    /// Yields every candidate file the pathspecs reach, each once, as a full path. A directory met
    /// on the way that cannot be listed is reported to <paramref name="warnings"/> as a file warning
    /// and passed over.
    /// </summary>
    IAsyncEnumerable<string> DiscoverAsync(ResolvedPathSpecs pathSpecs, IWarningSink warnings, CancellationToken cancellationToken);
}
