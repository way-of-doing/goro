namespace Goro.Discovery;

public interface IFileDiscoveryService
{
    IAsyncEnumerable<string> DiscoverAsync(IReadOnlyList<string> pathSpecs, CancellationToken cancellationToken);
}
