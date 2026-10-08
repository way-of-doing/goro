using Goro.Domain;
using Goro.Hashing;
using Goro.Pipeline;
using Goro.Tests.TestSupport;
using Goro.Warnings;
using Microsoft.Extensions.DependencyInjection;

namespace Goro.Tests.Cli;

/// <summary>
/// The <c>incomplete</c> warning end to end: one count per run, after the <c>unanswered</c> one,
/// exit code <c>12</c> under <c>--strict-exit-code</c>, and nothing at all under
/// <c>--no-warn=incomplete</c>. See docs/concepts/warnings.md.
/// </summary>
/// <remarks>
/// Nothing reads a file only in part until Goro has its own reader, so the real pipeline is wrapped
/// in one that reports every opened file named <c>partial*</c> as read in part.
/// </remarks>
public class IncompleteWarningTests
{
    private TempCollection _collection = null!;

    [SetUp]
    public void SetUp() => _collection = new TempCollection("goro-cli-incomplete-tests-");

    [TearDown]
    public void TearDown() => _collection.Dispose();

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunWithPartialFilesAsync(params string[] args) =>
        GoroAppFactory.Create(services => services.AddSingleton<IPipelinePlanner>(
            provider => new PartialReadingPlanner(new PipelinePlanner(provider.GetRequiredService<IAudioHasher>()))))
            .RunCapturedAsync(args);

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(params string[] args) =>
        GoroAppFactory.Create().RunCapturedAsync(args);

    [Test]
    public async Task Hash_AFileReadInPart_IsCountedOnceForTheRun_AndStillHashed()
    {
        _collection.Mp3("partial.mp3");
        _collection.Mp3("a.mp3");
        _collection.Mp3("b.mp3");

        var (exitCode, stdOut, stdErr) = await RunWithPartialFilesAsync("hash", "--strict-exit-code", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(12));
        Assert.That(Lines(stdOut), Has.Length.EqualTo(3).And.All.Not.EndWith(" -"));
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { new IncompleteWarning(1, 3).ToString() }));
    }

    [Test]
    public async Task Hash_AFileReadInPart_ReturnsZeroWithoutStrictExitCode()
    {
        _collection.Mp3("partial.mp3");

        var (exitCode, _, stdErr) = await RunWithPartialFilesAsync("hash", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { new IncompleteWarning(1, 1).ToString() }));
    }

    // An unreadable file has a warning of its own and is not counted among those opened; its code
    // outranks this one's.
    [Test]
    public async Task Hash_AFileReadInPart_BesideAnUnreadableOne_Returns13_AndCountsOnlyTheFilesOpened()
    {
        _collection.Mp3("partial.mp3");
        _collection.Mp3("a.mp3");
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, _, stdErr) = await RunWithPartialFilesAsync("hash", "--strict-exit-code", _collection.Root);

        var lines = Lines(stdErr);
        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(lines, Has.Length.EqualTo(2), stdErr);
        Assert.That(lines[0], Does.StartWith($"goro: warning: {bad}: cannot be read: "));
        Assert.That(lines[1], Is.EqualTo(new IncompleteWarning(1, 2).ToString()));
    }

    // Both warnings about the run come after every warning about a file, in the order of their codes.
    [Test]
    public async Task List_AFileReadInPartWhosePredicateWasUnanswered_IsCountedByBoth_UnansweredFirst()
    {
        var partial = _collection.Mp3("partial.mp3");

        var (exitCode, stdOut, stdErr) = await RunWithPartialFilesAsync(
            "list", "--strict-exit-code", "--filter=file::name AS NUMBER > 1 AND file::duration >= 0", _collection.Root);

        var lines = Lines(stdErr);
        Assert.That(exitCode, Is.EqualTo(12));
        Assert.That(stdOut, Is.Empty);
        Assert.That(lines, Has.Length.EqualTo(3), stdErr);
        Assert.That(lines[0], Does.StartWith($"goro: warning: {partial}: "));
        Assert.That(lines[1], Does.Contain("could not be answered"));
        Assert.That(lines[2], Is.EqualTo(new IncompleteWarning(1, 1).ToString()));
    }

    // A file the run never opened cannot have been read in part, whatever its name.
    [Test]
    public async Task List_APredicateOnTheSize_OpensNothing_SoNothingIsIncomplete()
    {
        _collection.Mp3("partial.mp3");

        var (exitCode, _, stdErr) = await RunWithPartialFilesAsync("list", "--strict-exit-code", "--filter=file::size >= 0", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task NoWarnIncomplete_IsIndistinguishableFromARunWithNothingReadInPart()
    {
        _collection.Mp3("partial.mp3");
        _collection.Mp3("a.mp3");

        var suppressed = await RunWithPartialFilesAsync("hash", "--strict-exit-code", "--no-warn=incomplete", _collection.Root);
        var clean = await RunAsync("hash", "--strict-exit-code", _collection.Root);

        Assert.That(suppressed.ExitCode, Is.EqualTo(clean.ExitCode).And.EqualTo(0));
        Assert.That(suppressed.StdErr, Is.EqualTo(clean.StdErr).And.Empty);
        Assert.That(Lines(suppressed.StdOut), Is.EquivalentTo(Lines(clean.StdOut)));
    }

    [Test]
    public async Task NoWarnIncomplete_LeavesTheOtherCategoriesAlone()
    {
        _collection.Mp3("partial.mp3");
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, _, stdErr) = await RunWithPartialFilesAsync("hash", "--strict-exit-code", "--no-warn=incomplete", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(Lines(stdErr), Has.Length.EqualTo(1).And.All.StartWith($"goro: warning: {bad}: cannot be read: "));
    }

    /// <summary>The real planner's stages, with every opened file named <c>partial*</c> read only in part.</summary>
    private sealed class PartialReadingPlanner(IPipelinePlanner inner) : IPipelinePlanner
    {
        public IPipelineStage<string, FileOutcome<HashResult>> PlanHash(HashOptions options) => new Partial<HashResult>(inner.PlanHash(options));

        public IPipelineStage<string, FileOutcome<ListResult>> PlanList(ListOptions options) => new Partial<ListResult>(inner.PlanList(options));
    }

    private sealed class Partial<TResult>(IPipelineStage<string, FileOutcome<TResult>> inner) : IPipelineStage<string, FileOutcome<TResult>>
        where TResult : class
    {
        public async Task<FileOutcome<TResult>> ExecuteAsync(string filePath, CancellationToken cancellationToken)
        {
            var outcome = await inner.ExecuteAsync(filePath, cancellationToken);
            if (!Path.GetFileName(filePath).StartsWith("partial", StringComparison.Ordinal) || outcome.Reading == FileReading.NotOpened)
            {
                return outcome;
            }

            return outcome.Disposition == FileDisposition.Matched
                ? FileOutcome<TResult>.Matched(outcome.Output!, outcome.Warnings, outcome.IsUnanswered, FileReading.ReadInPart)
                : FileOutcome<TResult>.Unmatched(outcome.Warnings, outcome.IsUnanswered, FileReading.ReadInPart);
        }
    }
}
