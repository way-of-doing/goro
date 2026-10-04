namespace Goro.Discovery;

/// <summary>
/// Pathspecs that have been checked and are known to be resolvable. Only
/// <see cref="IFileDiscoveryService.Resolve"/> can create one, so discovery can only be started
/// from pathspecs that were checked first, and every rejection happens before any output exists.
/// </summary>
public sealed class ResolvedPathSpecs
{
    internal ResolvedPathSpecs(IReadOnlyList<ResolvedPathSpec> entries) => Entries = entries;

    internal IReadOnlyList<ResolvedPathSpec> Entries { get; }
}

internal abstract record ResolvedPathSpec;

internal sealed record LiteralFile(string FullPath) : ResolvedPathSpec;

internal sealed record DirectoryTree(string FullPath) : ResolvedPathSpec;

/// <summary>
/// A glob matched against the entries of <paramref name="Directory"/> alone. Directory is null
/// when it does not exist, which is not an error: the glob simply matches nothing.
/// </summary>
internal sealed record Glob(string? Directory, string Pattern) : ResolvedPathSpec;
