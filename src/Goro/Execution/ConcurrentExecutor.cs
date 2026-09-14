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
/// A file that fails is reported to standard error and does not stop the rest of
/// the batch — at the scale Goro targets (100K+ files), one bad file shouldn't abort
/// an otherwise-successful run.
/// </summary>
public sealed class ConcurrentExecutor(int? maxDegreeOfParallelism = null) : IExecutor
{
    private readonly int _maxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;

    public async IAsyncEnumerable<TResult> ExecuteAsync<TResult>(
        IPipelineStage<string, TResult> pipeline,
        IAsyncEnumerable<string> filePaths,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
                        try
                        {
                            var result = await pipeline.ExecuteAsync(filePath, token);
                            await channel.Writer.WriteAsync(result, token);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            await Console.Error.WriteLineAsync($"goro: {filePath}: {ex.Message}");
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

        await pump;
    }
}
