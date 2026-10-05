using System.Runtime.CompilerServices;
using Goro.Messages;
using Goro.Warnings;

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

    // A directory walk lists one directory at a time, so that one it cannot list can be reported
    // and passed over while the rest of the walk carries on.
    private static readonly EnumerationOptions WalkOptions = new()
    {
        MatchType = MatchType.Simple,
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

        throw new PathSpecException(pathSpec, new ErrorMessage.PathSpecNotFound(new Code(pathSpec)));
    }

    private static Glob ResolveGlob(string pathSpec)
    {
        if (pathSpec.Count(c => c == '*') > 1)
        {
            throw new PathSpecException(pathSpec, new ErrorMessage.GlobWithSeveralStars(new Code(pathSpec)));
        }

        var directoryPart = Path.GetDirectoryName(pathSpec);
        if (directoryPart is not null && IsGlob(directoryPart))
        {
            throw new PathSpecException(pathSpec, new ErrorMessage.GlobWildcardBeforeLastComponent(new Code(pathSpec)));
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
            throw new PathSpecException(pathSpec, new ErrorMessage.PathSpecNotListable(new Code(pathSpec)), ex);
        }
    }

    public async IAsyncEnumerable<string> DiscoverAsync(
        ResolvedPathSpecs pathSpecs,
        IWarningSink warnings,
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
                DirectoryTree tree => Walk(tree.FullPath, warnings),
                Glob glob => Match(glob, warnings),
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

    // Walks a directory breadth first, one directory at a time. A directory met on the way that
    // cannot be listed is not an error: it produces one file warning, and the walk carries on with
    // the next entry (the FAILURES section of docs/concepts/pathspecs.md). Symbolic links to
    // directories are followed, as Directory.EnumerateFiles followed them before this walk replaced it.
    private static IEnumerable<string> Walk(string root, IWarningSink warnings)
    {
        var pending = new Queue<WalkedDirectory>([new WalkedDirectory(root, Canonicalize(root), null)]);
        while (pending.TryDequeue(out var directory))
        {
            if (TryList(directory.Path, "*", WalkOptions, warnings) is not { } entries)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (entry is not DirectoryInfo)
                {
                    yield return entry.FullName;
                }
                else if (entry.LinkTarget is null)
                {
                    pending.Enqueue(new WalkedDirectory(entry.FullName, Path.Combine(directory.Canonical, entry.Name), directory));
                }
                else
                {
                    // The cycle guard. A link back to the directory being listed, or to one of its
                    // ancestors on this walk, would have the walk descend through it until the
                    // operating system refuses the path: a spurious warning with one such link, and
                    // with two, a number of listings that doubles at every level. Such a link is
                    // passed over silently, since a cycle is part of the collection's shape, not a
                    // failure to read it. A link anywhere else is followed, even to a directory this
                    // walk has already reached by another route.
                    var target = Canonicalize(entry.FullName);
                    if (!directory.IsOnWalk(target))
                    {
                        pending.Enqueue(new WalkedDirectory(entry.FullName, target, directory));
                    }
                }
            }
        }
    }

    // Comparing canonical paths the way the platform compares names usually does: a false match only
    // passes over a link that already leads back onto the walk modulo case.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// A directory the walk has queued: the path it was reached by, its canonical path (with every
    /// symbolic link along it resolved), and the directory it was reached from.
    /// </summary>
    private sealed record WalkedDirectory(string Path, string Canonical, WalkedDirectory? Parent)
    {
        public bool IsOnWalk(string canonical)
        {
            for (var directory = this; directory is not null; directory = directory.Parent)
            {
                if (string.Equals(directory.Canonical, canonical, PathComparison))
                {
                    return true;
                }
            }

            return false;
        }
    }

    // The bound Linux puts on the symbolic links it resolves in one path, which ends a link that
    // leads back into itself.
    private const int MaxLinksResolved = 40;

    // The full path with every symbolic link along it resolved, as realpath(3) gives it: two paths to
    // the same directory then compare equal, however each was reached. A path that cannot be
    // resolved, such as a link that leads back into itself, is returned as it is; listing it will
    // then fail, and warn, the way any directory that cannot be listed does.
    private static string Canonicalize(string path)
    {
        try
        {
            var linksResolved = 0;
            return Canonicalize(Path.GetFullPath(path), ref linksResolved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Path.GetFullPath(path);
        }
    }

    private static string Canonicalize(string fullPath, ref int linksResolved)
    {
        var root = Path.GetPathRoot(fullPath)!;
        var resolved = root;
        foreach (var part in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(resolved, part);
            var info = new FileInfo(next);
            if (info.LinkTarget is not null)
            {
                if (++linksResolved > MaxLinksResolved)
                {
                    throw new IOException($"Too many levels of symbolic links: {fullPath}");
                }

                // The link's target may itself sit below further links, so it is resolved in turn.
                var target = info.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new IOException($"Cannot resolve symbolic link: {next}");
                next = Canonicalize(target.FullName, ref linksResolved);
            }

            resolved = next;
        }

        return resolved;
    }

    // Each entry a glob matches is treated as if it had been named directly: a directory is
    // walked in full, and a file is a file.
    private static IEnumerable<string> Match(Glob glob, IWarningSink warnings)
    {
        if (glob.Directory is null || TryList(glob.Directory, glob.Pattern, GlobOptions, warnings) is not { } entries)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            if (entry is DirectoryInfo)
            {
                foreach (var file in Walk(entry.FullName, warnings))
                {
                    yield return file;
                }
            }
            else
            {
                yield return entry.FullName;
            }
        }
    }

    // The entries of one directory, or null, with a file warning emitted, when it cannot be listed.
    // The entries are read in full here, since a listing can fail part way through as well as at
    // the start.
    private static List<FileSystemInfo>? TryList(string directory, string pattern, EnumerationOptions options, IWarningSink warnings)
    {
        try
        {
            return new DirectoryInfo(directory).EnumerateFileSystemInfos(pattern, options).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            warnings.Emit([FileWarning.From(directory, ex)]);
            return null;
        }
    }

    private static bool IsGlob(string pathSpec) => pathSpec.Contains('*') || pathSpec.Contains('?');
}
