using Goro.Pipeline;

namespace Goro.Execution;

/// <summary>
/// Invokes a pipeline stage for each input file, manages concurrency, records each file's outcome
/// in the run's tally, and streams the results that are to be rendered on to the output renderer.
/// </summary>
public interface IExecutor
{
    /// <summary>
    /// Yields the <see cref="FileOutcome{TResult}.Output"/> of every file that has one, in no
    /// particular order. A file that cannot be read is an outcome like any other; an exception
    /// escaping the pipeline is a defect, and fails the whole run.
    /// </summary>
    IAsyncEnumerable<TResult> ExecuteAsync<TResult>(
        IPipelineStage<string, FileOutcome<TResult>> pipeline,
        IAsyncEnumerable<string> filePaths,
        RunTally tally,
        CancellationToken cancellationToken)
        where TResult : class;
}
