using Goro.Tests.TestSupport;

namespace Goro.Tests.Cli;

/// <summary>
/// The "Warning suppression" class of docs/testing.md, for the rows that need no predicate, and the
/// parsing of the global options. A suppressed warning was not produced, so a run that suppresses one
/// must agree with a run where the condition never occurred in every channel at once: standard
/// output, standard error and exit code.
/// </summary>
public class WarningSuppressionTests
{
    private TempCollection _collection = null!;

    [SetUp]
    public void SetUp() => _collection = new TempCollection("goro-cli-suppression-tests-");

    [TearDown]
    public void TearDown() => _collection.Dispose();

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(params string[] args) =>
        GoroAppFactory.Create().RunCapturedAsync(args);

    // Output order is unspecified, so stdout is compared as a set of lines.
    private static void AssertIndistinguishable((int ExitCode, string StdOut, string StdErr) actual, (int ExitCode, string StdOut, string StdErr) expected)
    {
        Assert.That(actual.ExitCode, Is.EqualTo(expected.ExitCode), "exit code");
        Assert.That(actual.StdErr, Is.EqualTo(expected.StdErr), "standard error");
        Assert.That(actual.StdOut.Split('\n'), Is.EquivalentTo(expected.StdOut.Split('\n')), "standard output");
    }

    [Test]
    public async Task NoWarnFile_OverADirectoryThatCannotBeListed_IsIndistinguishableFromTheSameRunOverCleanData()
    {
        _collection.Mp3("a.mp3");
        _collection.Mp3("album", "b.mp3");
        var locked = _collection.Lock(_collection.Subdirectory("empty"));

        var warning = await RunAsync("list", "--strict-exit-code", _collection.Root);
        var suppressed = await RunAsync("list", "--strict-exit-code", "--no-warn=file", _collection.Root);
        _collection.Unlock(locked);
        var clean = await RunAsync("list", "--strict-exit-code", _collection.Root);

        Assert.That(warning.ExitCode, Is.EqualTo(11), "the scenario must actually warn");
        Assert.That(warning.StdErr, Does.Contain(locked));
        AssertIndistinguishable(suppressed, clean);
        Assert.That(clean.ExitCode, Is.EqualTo(0));
    }

    // hash.md still shows the unreadable file's row, with "-": that is the command's output, not a
    // warning, so only standard error and the exit code can agree with a clean run here.
    [Test]
    public async Task NoWarnFile_HashOverAnUnreadableFile_LeavesNoWarningAndNo11()
    {
        var bad = _collection.NotAudio("bad.mp3");
        var good = _collection.Mp3("good.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("hash", "--strict-exit-code", "--no-warn=file", bad, good);
        var clean = await RunAsync("hash", "--strict-exit-code", good);

        Assert.That(exitCode, Is.EqualTo(clean.ExitCode).And.EqualTo(0));
        Assert.That(stdErr, Is.EqualTo(clean.StdErr).And.Empty);
        Assert.That(stdOut, Does.Contain($"{bad} md5 -"));
    }

    [Test]
    public async Task NoWarnData_WhereAFileCouldNotBeRead_Still11_WithTheFileWarningPresent()
    {
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, _, stdErr) = await RunAsync("hash", "--strict-exit-code", "--no-warn=data", bad);

        Assert.That(exitCode, Is.EqualTo(11));
        Assert.That(stdErr, Does.Contain($"goro: warning: {bad}: cannot be read"));
    }

    [Test]
    public async Task NoWarn_BareAllAndDataFile_AreIdenticalInEveryChannel()
    {
        _collection.NotAudio("bad.mp3");
        _collection.Mp3("good.mp3");

        var bare = await RunAsync("hash", "--strict-exit-code", "--no-warn", _collection.Root);
        var trailing = await RunAsync("hash", "--strict-exit-code", _collection.Root, "--no-warn");
        var all = await RunAsync("hash", "--strict-exit-code", "--no-warn=all", _collection.Root);
        var both = await RunAsync("hash", "--strict-exit-code", "--no-warn=data,file", _collection.Root);

        Assert.That(bare.ExitCode, Is.EqualTo(0));
        Assert.That(bare.StdErr, Is.Empty);
        AssertIndistinguishable(trailing, bare);
        AssertIndistinguishable(all, bare);
        AssertIndistinguishable(both, bare);
    }

    [Test]
    public async Task NoWarnData_OnARunWithNoUnusableData_ChangesNothing()
    {
        _collection.NotAudio("bad.mp3");
        _collection.Mp3("good.mp3");

        var without = await RunAsync("hash", "--strict-exit-code", _collection.Root);
        var with = await RunAsync("hash", "--strict-exit-code", "--no-warn=data", _collection.Root);

        Assert.That(without.ExitCode, Is.EqualTo(11));
        AssertIndistinguishable(with, without);
    }

    [TestCase("list")]
    [TestCase("hash")]
    public async Task NoWarn_UnrecognisedCategory_IsRejectedWith2BeforeAnythingIsProcessed(string command)
    {
        _collection.Mp3("a.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(command, "--no-warn=data,tags", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Does.Contain("tags"));
    }

    [Test]
    public async Task NoWarn_CategoryInAnotherCase_IsAccepted()
    {
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("hash", "--strict-exit-code", "--no-warn=FiLe", bad);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut.Trim(), Is.EqualTo($"{bad} md5 -"));
        Assert.That(stdErr, Is.Empty);
    }

    // --- how the global options are read ---

    // The synopsis is --no-warn[=<category>,...]: a value is only ever attached with '=', so the
    // argument after a bare --no-warn is a pathspec, not a category list.
    [Test]
    public async Task NoWarn_Bare_FollowedByAPathspec_TakesThePathspecAsAPathspec()
    {
        var file = _collection.Mp3("a.mp3");

        var (exitCode, stdOut, _) = await RunAsync("list", "--no-warn", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut.Trim(), Is.EqualTo(file));
    }

    [Test]
    public async Task NoWarn_WithAttachedValue_FollowedByAPathspec()
    {
        var file = _collection.Mp3("a.mp3");

        var (exitCode, stdOut, _) = await RunAsync("list", "--no-warn=data", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut.Trim(), Is.EqualTo(file));
    }

    [Test]
    public async Task NoWarn_AfterTheSeparator_IsAPathspec()
    {
        var file = _collection.Mp3("--no-warn", "a.mp3");
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_collection.Root);
            var expected = Path.Combine(Directory.GetCurrentDirectory(), "--no-warn", Path.GetFileName(file));

            var (exitCode, stdOut, _) = await RunAsync("list", "--", "--no-warn");

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdOut.Trim(), Is.EqualTo(expected));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [TestCase("list")]
    [TestCase("hash")]
    public async Task UnknownOption_IsRejectedWith2_RatherThanSwallowingThePathspecAfterIt(string command)
    {
        _collection.Mp3("a.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync(command, "--strict-exitcode", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Does.Contain("strict-exitcode"));
    }
}
