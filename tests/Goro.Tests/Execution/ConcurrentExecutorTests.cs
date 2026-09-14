using Goro.Execution;
using Goro.Pipeline;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Execution;

public class ConcurrentExecutorTests
{
    private sealed class DelegateStage<TResult>(Func<string, CancellationToken, Task<TResult>> execute) : IPipelineStage<string, TResult>
    {
        public Task<TResult> ExecuteAsync(string filePath, CancellationToken cancellationToken) => execute(filePath, cancellationToken);
    }

    [Test]
    public async Task ExecuteAsync_RunsEachFileThroughThePipeline_AndYieldsAllResults()
    {
        var executor = new ConcurrentExecutor();
        var stage = new DelegateStage<string>((path, _) => Task.FromResult(path.ToUpperInvariant()));
        var files = new[] { "a.mp3", "b.mp3", "c.mp3" }.AsAsyncEnumerable();

        var results = await executor.ExecuteAsync(stage, files, CancellationToken.None).ToListAsync();

        Assert.That(results, Is.EquivalentTo(new[] { "A.MP3", "B.MP3", "C.MP3" }));
    }

    [Test]
    public async Task ExecuteAsync_RespectsMaxDegreeOfParallelism()
    {
        const int maxDegreeOfParallelism = 2;
        var executor = new ConcurrentExecutor(maxDegreeOfParallelism);

        var concurrentCount = 0;
        var observedMax = 0;
        var gate = new object();

        var stage = new DelegateStage<int>(async (_, ct) =>
        {
            int current;
            lock (gate)
            {
                concurrentCount++;
                current = concurrentCount;
                observedMax = Math.Max(observedMax, current);
            }

            await Task.Delay(50, ct);

            lock (gate)
            {
                concurrentCount--;
            }

            return current;
        });

        var files = Enumerable.Range(0, 8).Select(i => $"file{i}.mp3").AsAsyncEnumerable();

        await executor.ExecuteAsync(stage, files, CancellationToken.None).ToListAsync();

        Assert.That(observedMax, Is.LessThanOrEqualTo(maxDegreeOfParallelism));
    }

    [Test]
    public async Task ExecuteAsync_OneFileFailing_DoesNotStopTheRestOfTheBatch()
    {
        var executor = new ConcurrentExecutor();
        var stage = new DelegateStage<string>((path, _) =>
        {
            if (path == "bad.mp3")
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(path);
        });

        var files = new[] { "a.mp3", "bad.mp3", "b.mp3" }.AsAsyncEnumerable();

        var originalError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            var results = await executor.ExecuteAsync(stage, files, CancellationToken.None).ToListAsync();
            Assert.That(results, Is.EquivalentTo(new[] { "a.mp3", "b.mp3" }));
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Test]
    public void ExecuteAsync_Cancellation_StopsProcessing()
    {
        var executor = new ConcurrentExecutor();
        using var cts = new CancellationTokenSource();

        var stage = new DelegateStage<string>(async (path, ct) =>
        {
            await Task.Delay(200, ct);
            return path;
        });

        var files = Enumerable.Range(0, 20).Select(i => $"file{i}.mp3").AsAsyncEnumerable();

        cts.CancelAfter(20);

        Assert.CatchAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(stage, files, cts.Token).ToListAsync().AsTask());
    }
}
