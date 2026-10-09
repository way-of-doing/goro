using Goro.Domain;
using Goro.Hashing;
using Goro.Output;
using Goro.Pipeline;
using Goro.Predicates;
using Goro.Predicates.Evaluation;
using Goro.Warnings;

namespace Goro.Tests.Pipeline;

public class PipelinePlannerTests
{
    private sealed class FakeAudioHasher(Exception? failure = null) : IAudioHasher
    {
        public string? LastFilePath { get; private set; }

        public HashAlgorithmKind? LastAlgorithm { get; private set; }

        public Task<AudioHash> ComputeHashAsync(string filePath, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
        {
            LastFilePath = filePath;
            LastAlgorithm = algorithm;
            return failure is null ? Task.FromResult(new AudioHash("deadbeef", Incomplete: false)) : Task.FromException<AudioHash>(failure);
        }
    }

    [Test]
    public async Task PlanHash_ReturnsStageThatUsesTheConfiguredAlgorithm()
    {
        var hasher = new FakeAudioHasher();
        var planner = new PipelinePlanner(hasher);
        var options = new HashOptions(["."], HashAlgorithmKind.Sha1, OutputFormat.Plain);

        var stage = planner.PlanHash(options);
        var outcome = await stage.ExecuteAsync("/music/track.mp3", CancellationToken.None);

        Assert.That(hasher.LastFilePath, Is.EqualTo("/music/track.mp3"));
        Assert.That(hasher.LastAlgorithm, Is.EqualTo(HashAlgorithmKind.Sha1));
        Assert.That(outcome.Output, Is.EqualTo(new HashResult("/music/track.mp3", "sha1", "deadbeef")));
        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Matched));
        Assert.That(outcome.Warnings, Is.Empty);
    }

    // Whatever the hasher throws means the file cannot be read: the row stays, with its hash absent,
    // and the file carries exactly one file warning. See docs/commands/hash.md.
    [TestCaseSource(nameof(ReadFailures))]
    public async Task PlanHash_FileThatCannotBeRead_KeepsItsRowWithTheHashAbsent(Exception failure, string cause)
    {
        var planner = new PipelinePlanner(new FakeAudioHasher(failure));
        var options = new HashOptions(["."], HashAlgorithmKind.Md5, OutputFormat.Plain);

        var outcome = await planner.PlanHash(options).ExecuteAsync("/music/bad.mp3", CancellationToken.None);

        Assert.That(outcome.Output, Is.EqualTo(new HashResult("/music/bad.mp3", "md5", null)));
        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unreadable));
        Assert.That(outcome.Warnings, Is.EqualTo(new[] { new FileWarning("/music/bad.mp3", cause) }));
    }

    private static IEnumerable<TestCaseData> ReadFailures()
    {
        yield return new TestCaseData(new FileNotFoundException("Could not find file '/music/bad.mp3'."), "no such file or directory");
        yield return new TestCaseData(new UnauthorizedAccessException("Access to the path is denied."), "permission denied");
        yield return new TestCaseData(new InvalidDataException("no MPEG audio found"), "no MPEG audio found");
        yield return new TestCaseData(new IndexOutOfRangeException("Index was outside the bounds of the array."), "Index was outside the bounds of the array.");
    }

    [Test]
    public void PlanHash_Cancellation_IsNotAFileThatCannotBeRead()
    {
        var planner = new PipelinePlanner(new FakeAudioHasher(new OperationCanceledException()));
        var options = new HashOptions(["."], HashAlgorithmKind.Md5, OutputFormat.Plain);

        Assert.CatchAsync<OperationCanceledException>(() => planner.PlanHash(options).ExecuteAsync("/music/a.mp3", CancellationToken.None));
    }

    [Test]
    public async Task PlanList_ReturnsStageThatListsTheFileWithoutReadingIt()
    {
        var planner = new PipelinePlanner(new FakeAudioHasher());
        var options = new ListOptions(["."], OutputFormat.Plain);

        var stage = planner.PlanList(options);
        var outcome = await stage.ExecuteAsync("/music/does-not-exist.mp3", CancellationToken.None);

        Assert.That(outcome.Output, Is.EqualTo(new ListResult("/music/does-not-exist.mp3")));
        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Matched));
        Assert.That(outcome.Warnings, Is.Empty);
        Assert.That(stage, Is.InstanceOf<ListPipelineStage>());
    }

    [Test]
    public void PlanList_WithAFilter_ReturnsThePredicateStageForThatPredicate()
    {
        var predicate = new CompiledPredicate("TRUE", new Literal<bool>(true), SourceTable.Empty);
        var options = new ListOptions(["."], OutputFormat.Plain, predicate);

        var stage = new PipelinePlanner(new FakeAudioHasher()).PlanList(options);

        Assert.That(stage, Is.InstanceOf<PredicateStage>());
        Assert.That(((PredicateStage)stage).Predicate, Is.SameAs(predicate));
    }
}
