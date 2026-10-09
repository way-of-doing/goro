using Goro.Pipeline;
using Goro.Warnings;

namespace Goro.Execution;

/// <summary>
/// Gathers the run outcome as files finish, from any number of concurrent pipeline invocations:
/// each file's warnings go to the sink as one batch, and what the file counts towards is counted.
/// One tally serves one run.
/// </summary>
public sealed class RunTally(IWarningSink warnings)
{
    private int _found;
    private int _examined;
    private int _matched;
    private int _unanswered;
    private int _opened;
    private int _incomplete;

    public void Record<TResult>(FileOutcome<TResult> outcome)
        where TResult : class
    {
        if (!outcome.Warnings.IsEmpty)
        {
            warnings.Emit(outcome.Warnings);
        }

        Interlocked.Increment(ref _found);
        if (outcome.IsUnanswered)
        {
            Interlocked.Increment(ref _unanswered);
        }

        switch (outcome.Reading)
        {
            case FileReading.NotOpened:
                break;
            case FileReading.Read:
                Interlocked.Increment(ref _opened);
                break;
            case FileReading.ReadInPart:
                Interlocked.Increment(ref _opened);
                Interlocked.Increment(ref _incomplete);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Reading, "Unknown file reading.");
        }

        switch (outcome.Disposition)
        {
            case FileDisposition.Matched:
                Interlocked.Increment(ref _examined);
                Interlocked.Increment(ref _matched);
                break;
            case FileDisposition.Unmatched:
                Interlocked.Increment(ref _examined);
                break;
            case FileDisposition.Unreadable:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Disposition, "Unknown file disposition.");
        }
    }

    /// <summary>The outcome so far; read once the run has finished.</summary>
    public RunOutcome Outcome => new(
        Volatile.Read(ref _found),
        Volatile.Read(ref _examined),
        Volatile.Read(ref _matched),
        warnings.Produced,
        Volatile.Read(ref _unanswered),
        Volatile.Read(ref _opened),
        Volatile.Read(ref _incomplete));
}
