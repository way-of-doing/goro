namespace Goro.Cli;

/// <summary>
/// The exit codes of docs/concepts/exit-codes.md that Goro returns so far.
/// </summary>
public static class ExitCodes
{
    /// <summary>The run completed.</summary>
    public const int Completed = 0;

    /// <summary>The run never started: the command line, a pathspec, or the predicate was rejected.</summary>
    public const int Rejected = 2;
}
