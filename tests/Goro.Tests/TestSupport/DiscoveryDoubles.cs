using System.Runtime.CompilerServices;
using Goro.Discovery;
using Goro.Warnings;

namespace Goro.Tests.TestSupport;

/// <summary>Deletes one file after discovery has yielded it and before the pipeline reads it.</summary>
internal sealed class DeletingDiscovery(IFileDiscoveryService inner, string toDelete) : IFileDiscoveryService
{
    public ResolvedPathSpecs Resolve(IReadOnlyList<string> pathSpecs) => inner.Resolve(pathSpecs);

    public async IAsyncEnumerable<string> DiscoverAsync(
        ResolvedPathSpecs pathSpecs, IWarningSink warnings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var file in inner.DiscoverAsync(pathSpecs, warnings, cancellationToken))
        {
            if (file == toDelete)
            {
                File.Delete(file);
            }

            yield return file;
        }
    }
}

/// <summary>
/// Records whether a run touched the file system through discovery at all, for asserting that a
/// rejected run processed nothing.
/// </summary>
internal sealed class RecordingDiscovery(IFileDiscoveryService inner) : IFileDiscoveryService
{
    private int _resolved;
    private int _discovered;

    public bool Resolved => Volatile.Read(ref _resolved) > 0;

    public bool Discovered => Volatile.Read(ref _discovered) > 0;

    public ResolvedPathSpecs Resolve(IReadOnlyList<string> pathSpecs)
    {
        Interlocked.Increment(ref _resolved);
        return inner.Resolve(pathSpecs);
    }

    public IAsyncEnumerable<string> DiscoverAsync(ResolvedPathSpecs pathSpecs, IWarningSink warnings, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _discovered);
        return inner.DiscoverAsync(pathSpecs, warnings, cancellationToken);
    }
}
