using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Goro.Pipeline;

namespace Goro.Execution;

/// <summary>
/// Runs a pipeline stage over a set of files with bounded concurrency, using
/// <see cref="Parallel.ForEachAsync{TSource}(IAsyncEnumerable{TSource}, ParallelOptions, Func{TSource, CancellationToken, ValueTask})"/>.
/// Since that API only completes once every file has been processed, each worker
/// writes its result into a channel so this method can still stream results to the
/// caller as they complete, rather than buffering the whole batch.
///
/// A file that cannot be read does not stop the batch: the stage says so in the file's
/// <see cref="FileOutcome{TResult}"/>, which is recorded in the run's tally like any other, warnings
/// and all. Nothing here catches exceptions, because a stage that throws has not met a bad file but
/// a defect, and the run cannot be trusted to complete.
/// </summary>
public sealed class ConcurrentExecutor(int? maxDegreeOfParallelism = null) : IExecutor
{
    private readonly int _maxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;

    public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(
        IPipelineStage<string, FileOutcome<TResult>> pipeline,
        IAsyncEnumerable<string> filePaths,
        RunTally tally,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TResult : class
    {
        var channel = Channel.CreateUnbounded<TResult>();

        var pump = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(
                    filePaths,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                        CancellationToken = cancellationToken,
                    },
                    async (filePath, token) =>
                    {
                        var outcome = await pipeline.ExecuteAsync(filePath, token);
                        tally.Record(outcome);
                        if (outcome.Output is { } output)
                        {
                            await channel.Writer.WriteAsync(output, token);
                        }
                    });
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }

        // Rethrows whatever stopped the workers, so a failed run is never mistaken for a complete one.
        await pump;
    }
}
