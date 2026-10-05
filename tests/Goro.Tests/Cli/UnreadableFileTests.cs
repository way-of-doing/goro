using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Goro.Discovery;
using Goro.Domain;
using Goro.Pipeline;
using Goro.Tests.TestSupport;
using Goro.Warnings;
using Microsoft.Extensions.DependencyInjection;

namespace Goro.Tests.Cli;

/// <summary>
/// The "Unreadable files and pathspecs" class of docs/testing.md, for the rows that need no
/// predicate. What a file that cannot be read does to the output, standard error and exit code,
/// end to end.
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

        Assert.That(exitCode, Is.EqualTo(11));
        Assert.That(Lines(stdOut), Has.One.Match(HashedLine(good)).And.One.EqualTo($"{vanishing} md5 -"));
        AssertOneFileWarning(stdErr, vanishing);
        Assert.That(stdErr, Does.Contain("no such file or directory"));
    }

    [Test]
    public async Task Hash_OneUnreadableFileAmongManyReadable_Returns11UnderStrict_And0Without()
    {
        for (var i = 0; i < 10; i++)
        {
            _collection.Mp3($"good{i}.mp3");
        }

        _collection.NotAudio("bad.mp3");

        var (strictCode, strictOut, _) = await GoroAppFactory.Create().RunCapturedAsync("hash", "--strict-exit-code", _collection.Root);
        var (plainCode, plainOut, _) = await GoroAppFactory.Create().RunCapturedAsync("hash", _collection.Root);

        Assert.That(strictCode, Is.EqualTo(11));
        Assert.That(plainCode, Is.EqualTo(0));
        Assert.That(Lines(strictOut), Has.Length.EqualTo(11));
        Assert.That(Lines(plainOut), Is.EquivalentTo(Lines(strictOut)));
    }

    // An unreadable file does not count as examined, so a run whose only file was unreadable is not
    // one where "files were examined, but none of them matched".
    [Test]
    public async Task Hash_OnlyFileUnreadable_Returns11Not20()
    {
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, _) = await GoroAppFactory.Create().RunCapturedAsync("hash", "--strict-exit-code", bad);

        Assert.That(exitCode, Is.EqualTo(11));
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

    // --- pathspecs ---

    [TestCase(true, 11)]
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

    /// <summary>Deletes one file after discovery has yielded it and before the pipeline reads it.</summary>
    private sealed class DeletingDiscovery(IFileDiscoveryService inner, string toDelete) : IFileDiscoveryService
    {
        public ResolvedPathSpecs Resolve(IReadOnlyList<string> pathSpecs) => inner.Resolve(pathSpecs);

        public async IAsyncEnumerable<string> DiscoverAsync(
            ResolvedPathSpecs pathSpecs, IWarningSink warnings, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var file in inner.DiscoverAsync(pathSpecs, warnings, cancellationToken))
            {
                if (file == toDelete)
                {
                    File.Delete(file);
                }

                yield return file;
            }
        }
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
