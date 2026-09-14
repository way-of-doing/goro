using System.Runtime.CompilerServices;

namespace Goro.Discovery;

public sealed class FileDiscoveryService : IFileDiscoveryService
{
    // Only .mp3 is recognized for now; extend this list as more formats are supported.
    private static readonly string[] RecognizedExtensions = [".mp3"];

    public async IAsyncEnumerable<string> DiscoverAsync(
        IReadOnlyList<string> pathSpecs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        foreach (var pathSpec in pathSpecs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pathSpec.Contains('*') || pathSpec.Contains('?'))
            {
                foreach (var file in ResolveGlob(pathSpec))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return file;
                }
            }
            else if (Directory.Exists(pathSpec))
            {
                foreach (var file in Directory.EnumerateFiles(pathSpec, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsRecognized(file))
                    {
                        yield return Path.GetFullPath(file);
                    }
                }
            }
            else if (File.Exists(pathSpec))
            {
                if (IsRecognized(pathSpec))
                {
                    yield return Path.GetFullPath(pathSpec);
                }
            }
            else
            {
                throw new FileNotFoundException($"Pathspec not found: {pathSpec}", pathSpec);
            }
        }
    }

    // A pathspec containing '*'/'?' is split into a directory part and a filename
    // pattern, then matched recursively — so "*.mp3" with no directory prefix behaves
    // the same as pathspec "." once results are filtered to recognized extensions.
    private static IEnumerable<string> ResolveGlob(string pathSpec)
    {
        var directoryPart = Path.GetDirectoryName(pathSpec);
        var filePattern = Path.GetFileName(pathSpec);
        var directory = string.IsNullOrEmpty(directoryPart) ? "." : directoryPart;

        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, filePattern, SearchOption.AllDirectories))
        {
            if (IsRecognized(file))
            {
                yield return Path.GetFullPath(file);
            }
        }
    }

    private static bool IsRecognized(string filePath) =>
        RecognizedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
}
