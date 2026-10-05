using Goro.Warnings;

namespace Goro.Execution;

/// <summary>
/// What a completed run found, as far as the exit code needs to know it: how many files discovery
/// yielded, how many of them were examined (read as far as the command needed, rather than found
/// unreadable), how many of those matched, and which warning categories were produced after
/// suppression. See docs/concepts/exit-codes.md.
/// </summary>
public sealed record RunOutcome(int Found, int Examined, int Matched, IReadOnlySet<WarningCategory> Warned);
