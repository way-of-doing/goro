using System.Runtime.CompilerServices;

namespace Goro.Discovery;

public sealed class FileDiscoveryService : IFileDiscoveryService
{
    // A glob matches the entries of one directory with plain '*' and '?' (no Win32 quirks such
    // as "*.*"), keeps hidden entries as a directory walk does, and leaves case to the platform.
    private static readonly EnumerationOptions GlobOptions = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        RecurseSubdirectories = false,
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
    };

    public ResolvedPathSpecs Resolve(IReadOnlyList<string> pathSpecs) =>
        new(pathSpecs.Select(ResolveOne).ToList());

    private static ResolvedPathSpec ResolveOne(string pathSpec)
    {
        if (IsGlob(pathSpec))
        {
            return ResolveGlob(pathSpec);
        }

        if (Directory.Exists(pathSpec))
        {
            EnsureListable(pathSpec, pathSpec);
            return new DirectoryTree(Path.GetFullPath(pathSpec));
        }

        if (File.Exists(pathSpec))
        {
            return new LiteralFile(Path.GetFullPath(pathSpec));
        }

        throw new PathSpecException(pathSpec, $"Pathspec not found: {pathSpec}");
    }

    private static Glob ResolveGlob(string pathSpec)
    {
        if (pathSpec.Count(c => c == '*') > 1)
        {
            throw new PathSpecException(pathSpec, $"A glob may contain at most one '*': {pathSpec}");
        }

        var directoryPart = Path.GetDirectoryName(pathSpec);
        if (directoryPart is not null && IsGlob(directoryPart))
        {
            throw new PathSpecException(pathSpec, $"Wildcards may appear only in the last component of a glob: {pathSpec}");
        }

        var directory = string.IsNullOrEmpty(directoryPart) ? "." : directoryPart;
        if (!Directory.Exists(directory))
        {
            // Not an error: a glob in a directory that does not exist simply matches nothing.
            return new Glob(null, Path.GetFileName(pathSpec));
        }

        EnsureListable(pathSpec, directory);
        return new Glob(Path.GetFullPath(directory), Path.GetFileName(pathSpec));
    }

    // A directory that exists but cannot be listed at all is as knowable before the run as one
    // that does not exist, so it is rejected here rather than met halfway through the run.
    private static void EnsureListable(string pathSpec, string directory)
    {
        try
        {
            using var entries = Directory.EnumerateFileSystemEntries(directory).GetEnumerator();
            entries.MoveNext();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new PathSpecException(pathSpec, $"Pathspec cannot be listed: {pathSpec}", ex);
        }
    }

    public async IAsyncEnumerable<string> DiscoverAsync(
        ResolvedPathSpecs pathSpecs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        // Each pathspec is resolved independently, but the combined result across all
        // pathspecs is a set: a file matched by more than one pathspec (duplicate
        // pathspecs, overlapping directories, a glob that also matches a literal path,
        // etc.) is yielded only once. See docs/concepts/pathspecs.md.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in pathSpecs.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<string> files = entry switch
            {
                LiteralFile file => [file.FullPath],
                DirectoryTree tree => Walk(tree.FullPath),
                Glob glob => Match(glob),
                _ => throw new ArgumentOutOfRangeException(nameof(pathSpecs), entry, "Unknown kind of pathspec."),
            };

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CandidateFiles.IsCandidate(file) && seen.Add(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> Walk(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Select(Path.GetFullPath);

    // Each entry a glob matches is treated as if it had been named directly: a directory is
    // walked in full, and a file is a file.
    private static IEnumerable<string> Match(Glob glob)
    {
        if (glob.Directory is null)
        {
            yield break;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(glob.Directory, glob.Pattern, GlobOptions))
        {
            if (Directory.Exists(entry))
            {
                foreach (var file in Walk(entry))
                {
                    yield return file;
                }
            }
            else
            {
                yield return Path.GetFullPath(entry);
            }
        }
    }

    private static bool IsGlob(string pathSpec) => pathSpec.Contains('*') || pathSpec.Contains('?');
}
