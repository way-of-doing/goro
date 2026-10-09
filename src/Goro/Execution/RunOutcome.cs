using Goro.Warnings;

namespace Goro.Execution;

/// <summary>
/// What a completed run found, as far as the exit code needs to know it: how many files discovery
/// yielded, how many of them were examined (read as far as the command needed, rather than found
/// unreadable), how many of those matched, how many of those examined had a predicate that could not
/// be answered, how many were opened and how many of those could be read only in part, and which
/// warning categories were produced after suppression. See docs/concepts/exit-codes.md.
/// </summary>
public sealed record RunOutcome(
    int Found, int Examined, int Matched, IReadOnlySet<WarningCategory> Warned, int Unanswered = 0, int Opened = 0, int Incomplete = 0);
