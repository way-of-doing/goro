using Goro.Pipeline;

namespace Goro.Execution;

/// <summary>
/// Invokes a pipeline stage for each input file, manages concurrency, and streams
/// the resulting objects on to the output renderer.
/// </summary>
public interface IExecutor
{
    IAsyncEnumerable<TResult> ExecuteAsync<TResult>(
        IPipelineStage<string, TResult> pipeline,
        IAsyncEnumerable<string> filePaths,
        CancellationToken cancellationToken);
}
