using Goro.Execution;
using Goro.Warnings;

namespace Goro.Cli;

/// <summary>
/// The exit codes of docs/concepts/exit-codes.md, and the rules that choose one for a completed run.
/// </summary>
public static class ExitCodes
{
    /// <summary>The run completed; with <c>--strict-exit-code</c>, it had nothing further to report.</summary>
    public const int Completed = 0;

    /// <summary>The run started but could not be completed.</summary>
    public const int Failed = 1;

    /// <summary>The run never started: the command line, a pathspec, or the predicate was rejected.</summary>
    public const int Rejected = 2;

    /// <summary>Strict only: at least one data warning was emitted.</summary>
    public const int DataWarnings = 10;

    /// <summary>Strict only: the predicate could not be answered for at least one file, and the warning saying so was emitted.</summary>
    public const int UnansweredPredicates = 11;

    /// <summary>Strict only: at least one file warning was emitted.</summary>
    public const int FileWarnings = 12;

    /// <summary>Strict only: files were examined, but none of them matched.</summary>
    public const int NothingMatched = 20;

    /// <summary>Strict only: the pathspecs matched no files at all.</summary>
    public const int NothingFound = 21;

    /// <summary>
    /// The code a completed run returns. Without <paramref name="strict"/> that is always
    /// <see cref="Completed"/>. With it, the informational codes apply, and where several do, a code
    /// in the <c>1</c>x group beats one in the <c>2</c>x group and, within a group, the higher code
    /// wins.
    /// </summary>
    public static int For(RunOutcome outcome, bool strict)
    {
        if (!strict)
        {
            return Completed;
        }

        // The 1x group, highest first.
        if (outcome.Warned.Contains(WarningCategory.File))
        {
            return FileWarnings;
        }

        if (outcome.Warned.Contains(WarningCategory.Unanswered))
        {
            return UnansweredPredicates;
        }

        if (outcome.Warned.Contains(WarningCategory.Data))
        {
            return DataWarnings;
        }

        // The 2x group, highest first. The two cannot both apply. A file that could not be read is
        // found but not examined, so a run whose every file was unreadable is neither.
        if (outcome.Found == 0)
        {
            return NothingFound;
        }

        if (outcome.Examined > 0 && outcome.Matched == 0)
        {
            return NothingMatched;
        }

        return Completed;
    }
}
