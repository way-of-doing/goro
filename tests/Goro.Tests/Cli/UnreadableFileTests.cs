using System.Text.Json;
using System.Text.RegularExpressions;
using Goro.Discovery;
using Goro.Domain;
using Goro.Pipeline;
using Goro.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Goro.Tests.Cli;

/// <summary>
/// The "Unreadable files and pathspecs" class of docs/testing.md. What a file that cannot be read
/// does to the output, standard error and exit code, end to end, with and without a predicate. The
/// rows that need a tag identifier wait for the tag namespaces; their counterparts here use
/// <c>file::duration</c>, which needs the file read in the same way.
/// </summary>
public class UnreadableFileTests
{
    private TempCollection _collection = null!;

    [SetUp]
    public void SetUp() => _collection = new TempCollection("goro-cli-unreadable-tests-");

    [TearDown]
    public void TearDown() => _collection.Dispose();

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string HashedLine(string path) => $@"^{Regex.Escape(path)} md5 [0-9a-f]{{32}}$";

    private static void AssertOneFileWarning(string stdErr, string path)
    {
        var lines = Lines(stdErr);
        Assert.That(lines, Has.Length.EqualTo(1), stdErr);
        Assert.That(lines[0], Does.StartWith($"goro: warning: {path}: cannot be read: "));
    }

    // --- goro hash: the row must appear, with its hash absent ---

    public enum UnreadableKind
    {
        PermissionDenied,
        NotAudioAtAll,
        CutShort,
    }

    private string MakeUnreadable([Values] UnreadableKind kind) => kind switch
    {
        UnreadableKind.PermissionDenied => _collection.Lock(_collection.Mp3("locked.mp3")),
        UnreadableKind.NotAudioAtAll => _collection.NotAudio("text.mp3"),
        UnreadableKind.CutShort => _collection.CutShortInsideItsTag("cut.mp3"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Test]
    public async Task Hash_Plain_FileThatCannotBeRead_KeepsItsRowWithADash_WarnsOnce_AndTheRunCompletes([Values] UnreadableKind kind)
    {
        var good = _collection.Mp3("good.mp3");
        var bad = MakeUnreadable(kind);

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync("hash", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        var lines = Lines(stdOut);
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines, Has.One.Match(HashedLine(good)));
        Assert.That(lines, Has.One.EqualTo($"{bad} md5 -"));
        AssertOneFileWarning(stdErr, bad);
    }

    [Test]
    public async Task Hash_Json_FileThatCannotBeRead_KeepsItsObjectWithANullHash_AndWarnsOnce([Values] UnreadableKind kind)
    {
        var good = _collection.Mp3("good.mp3");
        var bad = MakeUnreadable(kind);

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync("hash", "-o", "json", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        using var document = JsonDocument.Parse(stdOut);
        var entries = document.RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("file").GetString()!);
        Assert.That(entries.Keys, Is.EquivalentTo(new[] { good, bad }));
        Assert.That(entries[good].GetProperty("hash").GetString(), Has.Length.EqualTo(32));
        Assert.That(entries[bad].GetProperty("algo").GetString(), Is.EqualTo("md5"));
        Assert.That(entries[bad].GetProperty("hash").ValueKind, Is.EqualTo(JsonValueKind.Null));
        AssertOneFileWarning(stdErr, bad);
    }

    // The window between discovery and processing is real on a large collection.
    [Test]
    public async Task Hash_FileRemovedBetweenDiscoveryAndProcessing_IsTreatedLikeAnyOtherUnreadableFile()
    {
        var good = _collection.Mp3("good.mp3");
        var vanishing = _collection.Mp3("vanishing.mp3");
        var app = GoroAppFactory.Create(services =>
            services.AddSingleton<IFileDiscoveryService>(new DeletingDiscovery(new FileDiscoveryService(), vanishing)));

        var (exitCode, stdOut, stdErr) = await app.RunCapturedAsync("hash", "--strict-exit-code", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(Lines(stdOut), Has.One.Match(HashedLine(good)).And.One.EqualTo($"{vanishing} md5 -"));
        AssertOneFileWarning(stdErr, vanishing);
        Assert.That(stdErr, Does.Contain("no such file or directory"));
    }

    [Test]
    public async Task Hash_OneUnreadableFileAmongManyReadable_Returns12UnderStrict_And0Without()
    {
        for (var i = 0; i < 10; i++)
        {
            _collection.Mp3($"good{i}.mp3");
        }

        _collection.NotAudio("bad.mp3");

        var (strictCode, strictOut, _) = await GoroAppFactory.Create().RunCapturedAsync("hash", "--strict-exit-code", _collection.Root);
        var (plainCode, plainOut, _) = await GoroAppFactory.Create().RunCapturedAsync("hash", _collection.Root);

        Assert.That(strictCode, Is.EqualTo(13));
        Assert.That(plainCode, Is.EqualTo(0));
        Assert.That(Lines(strictOut), Has.Length.EqualTo(11));
        Assert.That(Lines(plainOut), Is.EquivalentTo(Lines(strictOut)));
    }

    // An unreadable file does not count as examined, so a run whose only file was unreadable is not
    // one where "files were examined, but none of them matched".
    [Test]
    public async Task Hash_OnlyFileUnreadable_Returns12Not20()
    {
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, _) = await GoroAppFactory.Create().RunCapturedAsync("hash", "--strict-exit-code", bad);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(stdOut.Trim(), Is.EqualTo($"{bad} md5 -"));
    }

    [Test]
    public async Task Hash_ReadableFilesOnly_Returns0UnderStrict()
    {
        _collection.Mp3("a.mp3");

        var (exitCode, _, stdErr) = await GoroAppFactory.Create().RunCapturedAsync("hash", "--strict-exit-code", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdErr, Is.Empty);
    }

    // --- goro list without a filter reads nothing, so nothing can be unreadable ---

    [Test]
    public async Task List_NoFilter_FileThatCannotBeOpened_IsListedWithoutAWarning()
    {
        var locked = _collection.Lock(_collection.Mp3("locked.mp3"));

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync("list", "--strict-exit-code", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut.Trim(), Is.EqualTo(locked));
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task List_NoFilter_Mp3HoldingNoAudio_IsListedWithoutAWarning()
    {
        var notAudio = _collection.NotAudio("text.mp3");

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync("list", "--strict-exit-code", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut.Trim(), Is.EqualTo(notAudio));
        Assert.That(stdErr, Is.Empty);
    }

    // --- goro list --filter: a file that cannot be read is left out, with one warning ---

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(params string[] args) =>
        GoroAppFactory.Create().RunCapturedAsync(args);

    /// <summary>The run's one warning about predicates that could not be answered, given what follows "for".</summary>
    private static string UnansweredLine(string rest) => $"goro: warning: the predicate could not be answered for {rest}";

    private static string DataWarningLine(string path, string subExpression) =>
        $"goro: warning: {path}: cannot interpret the data of {subExpression}";

    [Test]
    public async Task List_Filter_FileThatCannotBeRead_IsNotListed_WarnsOnce_AndTheRunCompletes([Values] UnreadableKind kind)
    {
        var good = _collection.Mp3("good.mp3");
        var bad = MakeUnreadable(kind);

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--filter=file::duration >= 0", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { good }));
        AssertOneFileWarning(stdErr, bad);
    }

    [Test]
    public async Task List_Filter_FileRemovedBetweenDiscoveryAndProcessing_IsNotListed_AndWarnsOnce()
    {
        var good = _collection.Mp3("good.mp3");
        var vanishing = _collection.Mp3("vanishing.mp3");
        var app = GoroAppFactory.Create(services =>
            services.AddSingleton<IFileDiscoveryService>(new DeletingDiscovery(new FileDiscoveryService(), vanishing)));

        var (exitCode, stdOut, stdErr) = await app.RunCapturedAsync("list", "--strict-exit-code", "--filter=file::size >= 0", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { good }));
        AssertOneFileWarning(stdErr, vanishing);
        Assert.That(stdErr, Does.Contain("no such file or directory"));
    }

    // Nothing had to be read, so nothing could fail to be read.
    [TestCase(@"file::path =~ r""locked\.mp3$""")]
    [TestCase(@"file::name == ""locked.mp3""")]
    [TestCase(@"file::extension == ""mp3""")]
    public async Task List_Filter_OnlyPathNameOrExtension_OverAFileThatCannotBeOpened_DoesNotWarn_AndEvaluates(string predicate)
    {
        var locked = _collection.Lock(_collection.Mp3("locked.mp3"));

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", $"--filter={predicate}", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { locked }));
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task List_Filter_SizeOnly_OverAFileWhoseContentsCannotBeParsed_DoesNotWarn()
    {
        var notAudio = _collection.NotAudio("text.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--filter=file::size > 0", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { notAudio }));
        Assert.That(stdErr, Is.Empty);
    }

    // testing.md writes this row with `artist == "x"`; file::duration needs the file read the same way.
    [Test]
    public async Task List_Filter_UninterpretableDataThenAFileThatCannotBeRead_GivesOneFileWarningAndNoDataWarning()
    {
        var notAudio = _collection.NotAudio("text.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(
            "list", "--strict-exit-code", "--filter=file::name AS NUMBER > 1 OR file::duration > 0", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(stdOut, Is.Empty);
        AssertOneFileWarning(stdErr, notAudio);
    }

    // --- goro list --filter: a predicate without an answer ---

    [Test]
    public async Task List_Filter_PredicateUnusable_IsNotListed_AndTheDataWarningIsOnStandardError()
    {
        var file = _collection.Mp3("track.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--filter=file::name AS NUMBER > 1", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut, Is.Empty);
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { DataWarningLine(file, "file::name AS NUMBER"), UnansweredLine("the one file examined, which was not listed") }));
    }

    [Test]
    public async Task List_Filter_PredicateUnusableForSomeFiles_EndsTheRunWithOneWarningCountingThem()
    {
        // No name is a number, so every file warns; the two whose names begin with a digit are
        // answered by the second operand, and the other two are left without an answer.
        string[] files = [_collection.Mp3("1.mp3"), _collection.Mp3("2.mp3"), _collection.Mp3("one.mp3"), _collection.Mp3("two.mp3")];

        var (exitCode, stdOut, stdErr) = await RunAsync(
            "list", "--strict-exit-code", @"--filter=file::name AS NUMBER > 0 OR file::name =~ r""^[0-9]""", _collection.Root);

        var lines = Lines(stdErr);
        Assert.That(exitCode, Is.EqualTo(11));
        Assert.That(Lines(stdOut), Is.EquivalentTo(files[..2]));
        Assert.That(lines, Has.Length.EqualTo(5), stdErr);
        Assert.That(lines[..4], Is.EquivalentTo(files.Select(file => DataWarningLine(file, "file::name AS NUMBER"))));
        Assert.That(lines[^1], Is.EqualTo(UnansweredLine("2 of the 4 files examined, which were not listed")));
    }

    // The predicate, not the command, decides here. FALLBACK replaces the comparison's result but
    // cannot undo the warning the comparison emitted while producing it.
    [Test]
    public async Task List_Filter_PredicateUnusable_WrappedInFallbackToTrue_IsListed()
    {
        var file = _collection.Mp3("track.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--filter=FALLBACK(file::name AS NUMBER > 1, TRUE)", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { file }));
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { DataWarningLine(file, "file::name AS NUMBER") }), "the predicate answered, so nothing is unanswered");
    }

    // --- goro list --filter: exit codes, which only --strict-exit-code surfaces ---

    private static string[] ListArgs(bool strict, string predicate, string root) =>
        strict ? ["list", "--strict-exit-code", $"--filter={predicate}", root] : ["list", $"--filter={predicate}", root];

    [Test]
    public async Task List_Filter_OneUnreadableFileAmongManyReadable_Returns12([Values] bool strict)
    {
        for (var i = 0; i < 10; i++)
        {
            _collection.Mp3($"good{i}.mp3");
        }

        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(ListArgs(strict, "file::duration >= 0", _collection.Root));

        Assert.That(exitCode, Is.EqualTo(strict ? 13 : 0));
        Assert.That(Lines(stdOut), Has.Length.EqualTo(10).And.No.EqualTo(bad));
        AssertOneFileWarning(stdErr, bad);
    }

    // Within a group the higher-numbered code wins. The readable file's name is not a number, which
    // warns; the unreadable file says only that it could not be read.
    [Test]
    public async Task List_Filter_UnreadableFileAndUninterpretableData_Returns12Not10([Values] bool strict)
    {
        var good = _collection.Mp3("good.mp3");
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(ListArgs(strict, "file::name AS NUMBER > 1 OR file::duration >= 0", _collection.Root));

        Assert.That(exitCode, Is.EqualTo(strict ? 13 : 0));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { good }));
        var lines = Lines(stdErr);
        Assert.That(lines, Has.Length.EqualTo(2), stdErr);
        Assert.That(lines, Has.One.EqualTo(DataWarningLine(good, "file::name AS NUMBER")));
        Assert.That(lines, Has.One.StartsWith($"goro: warning: {bad}: cannot be read: "));
    }

    // An unreadable file does not count as examined.
    [Test]
    public async Task List_Filter_UnreadableFileWhereNothingMatched_Returns12Not20([Values] bool strict)
    {
        _collection.Mp3("good.mp3");
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(ListArgs(strict, "file::duration > 100000", _collection.Root));

        Assert.That(exitCode, Is.EqualTo(strict ? 13 : 0));
        Assert.That(stdOut, Is.Empty);
        AssertOneFileWarning(stdErr, bad);
    }

    // The 1x group takes precedence over the 2x group. The data warning fires, but the AND is settled
    // by its false second operand, so the predicate is answered.
    [Test]
    public async Task List_Filter_DataWarningAndAnEmptyResult_Returns10Not20([Values] bool strict)
    {
        var file = _collection.Mp3("track.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(ListArgs(strict, "file::name AS NUMBER > 1 AND file::size < 0", _collection.Root));

        Assert.That(exitCode, Is.EqualTo(strict ? 10 : 0));
        Assert.That(stdOut, Is.Empty);
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { DataWarningLine(file, "file::name AS NUMBER") }));
    }

    // An unanswered predicate outranks the data warnings that caused it, and the 2x group.
    [Test]
    public async Task List_Filter_UnansweredAndAnEmptyResult_Returns11Not10Or20([Values] bool strict)
    {
        _collection.Mp3("track.mp3");

        var (exitCode, stdOut, _) = await RunAsync(ListArgs(strict, "file::name AS NUMBER > 1", _collection.Root));

        Assert.That(exitCode, Is.EqualTo(strict ? 11 : 0));
        Assert.That(stdOut, Is.Empty);
    }

    // An unreadable file has its own warning, is not counted as unanswered, and outranks the summary.
    [Test]
    public async Task List_Filter_UnansweredAndAnUnreadableFile_Returns12_AndCountsOnlyTheFileExamined([Values] bool strict)
    {
        var good = _collection.Mp3("good.mp3");
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(ListArgs(strict, "file::name AS NUMBER > 1 AND file::duration >= 0", _collection.Root));

        Assert.That(exitCode, Is.EqualTo(strict ? 13 : 0));
        Assert.That(stdOut, Is.Empty);
        var lines = Lines(stdErr);
        Assert.That(lines, Has.Length.EqualTo(3), stdErr);
        Assert.That(lines, Has.One.EqualTo(DataWarningLine(good, "file::name AS NUMBER")));
        Assert.That(lines, Has.One.StartsWith($"goro: warning: {bad}: cannot be read: "));
        Assert.That(lines[^1], Is.EqualTo(UnansweredLine("the one file examined, which was not listed")));
    }

    // --- pathspecs ---

    [TestCase(true, 13)]
    [TestCase(false, 0)]
    public async Task List_SubdirectoryThatCannotBeListed_WarnsAndEverySiblingIsStillListed(bool strict, int expectedCode)
    {
        var before = _collection.Mp3("a.mp3");
        var sibling = _collection.Mp3("album", "b.mp3");
        _collection.Mp3("locked", "hidden.mp3");
        var locked = _collection.Lock(_collection.PathOf("locked"));
        string[] args = strict ? ["list", "--strict-exit-code", _collection.Root] : ["list", _collection.Root];

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync(args);

        Assert.That(exitCode, Is.EqualTo(expectedCode));
        Assert.That(Lines(stdOut), Is.EquivalentTo(new[] { before, sibling }));
        AssertOneFileWarning(stdErr, locked);
    }

    [Test]
    public async Task GlobMatchingNothing_IsNotAnError_AndReturns21UnderStrict()
    {
        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync("list", "--strict-exit-code", _collection.PathOf("*.mp3"));

        Assert.That(exitCode, Is.EqualTo(21));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Is.Empty);
    }

    [TestCase("list")]
    [TestCase("hash")]
    public async Task FileWithAnotherExtensionNamedAlone_IsNotAnError_AndReturns21UnderStrict(string command)
    {
        var cover = _collection.NotAudio("cover.jpg");

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync(command, "--strict-exit-code", cover);

        Assert.That(exitCode, Is.EqualTo(21));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Is.Empty);
    }

    // The option never changes how failures are reported.
    [TestCase("list")]
    [TestCase("hash")]
    public async Task MissingPathspec_UnderStrict_IsStillRejectedWith2(string command)
    {
        var (exitCode, stdOut, _) = await GoroAppFactory.Create().RunCapturedAsync(command, "--strict-exit-code", _collection.PathOf("missing.mp3"));

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdOut, Is.Empty);
    }

    // A stage that throws has met a defect, not a bad file: the run could not be completed.
    [Test]
    public async Task ExceptionEscapingThePipeline_FailsTheRunWith1()
    {
        _collection.Mp3("a.mp3");
        var app = GoroAppFactory.Create(services => services.AddSingleton<IPipelinePlanner>(new ThrowingPlanner()));

        var (exitCode, _, stdErr) = await app.RunCapturedAsync("list", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(stdErr, Does.Contain("goro: error: defect"));
    }

    private sealed class ThrowingPlanner : IPipelinePlanner
    {
        public IPipelineStage<string, FileOutcome<HashResult>> PlanHash(HashOptions options) => new ThrowingStage<HashResult>();

        public IPipelineStage<string, FileOutcome<ListResult>> PlanList(ListOptions options) => new ThrowingStage<ListResult>();

        private sealed class ThrowingStage<T> : IPipelineStage<string, FileOutcome<T>>
            where T : class
        {
            public Task<FileOutcome<T>> ExecuteAsync(string input, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("defect");
        }
    }
}
