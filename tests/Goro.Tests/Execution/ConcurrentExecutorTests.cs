using Goro.Execution;
using Goro.Pipeline;
using Goro.Tests.TestSupport;
using Goro.Warnings;

namespace Goro.Tests.Execution;

public class ConcurrentExecutorTests
{
    private sealed class DelegateStage<TResult>(Func<string, CancellationToken, Task<FileOutcome<TResult>>> execute)
        : IPipelineStage<string, FileOutcome<TResult>>
        where TResult : class
    {
        public Task<FileOutcome<TResult>> ExecuteAsync(string filePath, CancellationToken cancellationToken) => execute(filePath, cancellationToken);
    }

    private static DelegateStage<string> Matching(Func<string, string> map) =>
        new((path, _) => Task.FromResult(FileOutcome<string>.Matched(map(path))));

    private RecordingWarningSink _warnings = null!;
    private RunTally _tally = null!;

    [SetUp]
    public void SetUp()
    {
        _warnings = new RecordingWarningSink();
        _tally = new RunTally(_warnings);
    }

    [Test]
    public async Task ExecuteAsync_RunsEachFileThroughThePipeline_AndYieldsAllResults()
    {
        var executor = new ConcurrentExecutor();
        var files = new[] { "a.mp3", "b.mp3", "c.mp3" }.AsAsyncEnumerable();

        var results = await executor.ExecuteAsync(Matching(p => p.ToUpperInvariant()), files, _tally, CancellationToken.None).ToListAsync();

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

        var stage = new DelegateStage<string>(async (path, ct) =>
        {
            lock (gate)
            {
                concurrentCount++;
                observedMax = Math.Max(observedMax, concurrentCount);
            }

            await Task.Delay(50, ct);

            lock (gate)
            {
                concurrentCount--;
            }

            return FileOutcome<string>.Matched(path);
        });

        var files = Enumerable.Range(0, 8).Select(i => $"file{i}.mp3").AsAsyncEnumerable();

        await executor.ExecuteAsync(stage, files, _tally, CancellationToken.None).ToListAsync();

        Assert.That(observedMax, Is.LessThanOrEqualTo(maxDegreeOfParallelism));
    }

    [Test]
    public async Task ExecuteAsync_FileThatCannotBeRead_IsRecordedAndDoesNotStopTheRestOfTheBatch()
    {
        var executor = new ConcurrentExecutor();
        var stage = new DelegateStage<string>((path, _) => Task.FromResult(path == "bad.mp3"
            ? FileOutcome<string>.Unreadable(new FileWarning(path, "damaged"))
            : FileOutcome<string>.Matched(path)));

        var files = new[] { "a.mp3", "bad.mp3", "b.mp3" }.AsAsyncEnumerable();

        var results = await executor.ExecuteAsync(stage, files, _tally, CancellationToken.None).ToListAsync();

        Assert.That(results, Is.EquivalentTo(new[] { "a.mp3", "b.mp3" }));
        Assert.That(_warnings.Warnings, Is.EqualTo(new[] { new FileWarning("bad.mp3", "damaged") }));
        Assert.That(_tally.Outcome, Has.Property(nameof(RunOutcome.Found)).EqualTo(3)
            .And.Property(nameof(RunOutcome.Examined)).EqualTo(2)
            .And.Property(nameof(RunOutcome.Matched)).EqualTo(2));
    }

    [Test]
    public async Task ExecuteAsync_OutcomeWithoutOutput_IsCountedButNotYielded()
    {
        var executor = new ConcurrentExecutor();
        var stage = new DelegateStage<string>((path, _) => Task.FromResult(path == "kept.mp3"
            ? FileOutcome<string>.Matched(path)
            : FileOutcome<string>.Unmatched()));

        var files = new[] { "kept.mp3", "dropped.mp3" }.AsAsyncEnumerable();

        var results = await executor.ExecuteAsync(stage, files, _tally, CancellationToken.None).ToListAsync();

        Assert.That(results, Is.EqualTo(new[] { "kept.mp3" }));
        Assert.That(_tally.Outcome, Has.Property(nameof(RunOutcome.Examined)).EqualTo(2)
            .And.Property(nameof(RunOutcome.Matched)).EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_EachFilesWarnings_ReachTheSinkAsOneBatch()
    {
        var executor = new ConcurrentExecutor();
        var stage = new DelegateStage<string>((path, _) => Task.FromResult(FileOutcome<string>.Unmatched(
            [new DataWarning(path, "NUMBER(x)"), new DataWarning(path, "y")])));

        var files = Enumerable.Range(0, 20).Select(i => $"file{i}.mp3").AsAsyncEnumerable();

        await executor.ExecuteAsync(stage, files, _tally, CancellationToken.None).ToListAsync();

        Assert.That(_warnings.Batches, Has.Count.EqualTo(20));
        Assert.That(_warnings.Batches.Select(b => b.Count), Has.All.EqualTo(2));
        Assert.That(_warnings.Batches.Select(b => b.Select(w => ((PathWarning)w).Path).Distinct().Count()), Has.All.EqualTo(1));
    }

    // A stage reports a file it cannot read through its outcome; an exception means something is
    // wrong with Goro, and the run must not carry on as though it were complete.
    [Test]
    public void ExecuteAsync_StageThatThrows_FailsTheRun()
    {
        var executor = new ConcurrentExecutor();
        var stage = new DelegateStage<string>((path, _) => path == "bad.mp3"
            ? throw new InvalidOperationException("boom")
            : Task.FromResult(FileOutcome<string>.Matched(path)));

        var files = new[] { "a.mp3", "bad.mp3", "b.mp3" }.AsAsyncEnumerable();

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(stage, files, _tally, CancellationToken.None).ToListAsync().AsTask());
    }

    [Test]
    public void ExecuteAsync_Cancellation_StopsProcessing()
    {
        var executor = new ConcurrentExecutor();
        using var cts = new CancellationTokenSource();

        var stage = new DelegateStage<string>(async (path, ct) =>
        {
            await Task.Delay(200, ct);
            return FileOutcome<string>.Matched(path);
        });

        var files = Enumerable.Range(0, 20).Select(i => $"file{i}.mp3").AsAsyncEnumerable();

        cts.CancelAfter(20);

        Assert.CatchAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(stage, files, _tally, cts.Token).ToListAsync().AsTask());
    }
}
