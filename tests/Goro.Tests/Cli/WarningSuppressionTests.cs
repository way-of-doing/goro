using Goro.Tests.TestSupport;

namespace Goro.Tests.Cli;

/// <summary>
/// The "Warning suppression" class of docs/testing.md, and the parsing of the global options. A suppressed warning was not produced, so a run that suppresses one
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

        Assert.That(warning.ExitCode, Is.EqualTo(13), "the scenario must actually warn");
        Assert.That(warning.StdErr, Does.Contain(locked));
        AssertIndistinguishable(suppressed, clean);
        Assert.That(clean.ExitCode, Is.EqualTo(0));
    }

    // hash.md still shows the unreadable file's row, with "-": that is the command's output, not a
    // warning, so only standard error and the exit code can agree with a clean run here.
    [Test]
    public async Task NoWarnFile_HashOverAnUnreadableFile_LeavesNoWarningAndNo12()
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
    public async Task NoWarnData_WhereAFileCouldNotBeRead_Still12_WithTheFileWarningPresent()
    {
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, _, stdErr) = await RunAsync("hash", "--strict-exit-code", "--no-warn=data", bad);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(stdErr, Does.Contain($"goro: warning: {bad}: cannot be read"));
    }

    // goro hash takes no predicate, so it never warns about one going unanswered, and data,file
    // suppresses everything it can say.
    [Test]
    public async Task NoWarn_AllAndDataFile_AttachedOrSeparate_AreIdenticalInEveryChannel()
    {
        _collection.NotAudio("bad.mp3");
        _collection.Mp3("good.mp3");

        var all = await RunAsync("hash", "--strict-exit-code", "--no-warn=all", _collection.Root);
        var separate = await RunAsync("hash", "--strict-exit-code", "--no-warn", "all", _collection.Root);
        var trailing = await RunAsync("hash", "--strict-exit-code", _collection.Root, "--no-warn", "all");
        var both = await RunAsync("hash", "--strict-exit-code", "--no-warn=data,file", _collection.Root);
        var bothSeparate = await RunAsync("hash", "--strict-exit-code", "--no-warn", "data,file", _collection.Root);

        Assert.That(all.ExitCode, Is.EqualTo(0));
        Assert.That(all.StdErr, Is.Empty);
        AssertIndistinguishable(separate, all);
        AssertIndistinguishable(trailing, all);
        AssertIndistinguishable(both, all);
        AssertIndistinguishable(bothSeparate, all);
    }

    [Test]
    public async Task NoWarnData_OnARunWithNoUnusableData_ChangesNothing()
    {
        _collection.NotAudio("bad.mp3");
        _collection.Mp3("good.mp3");

        var without = await RunAsync("hash", "--strict-exit-code", _collection.Root);
        var with = await RunAsync("hash", "--strict-exit-code", "--no-warn=data", _collection.Root);

        Assert.That(without.ExitCode, Is.EqualTo(13));
        AssertIndistinguishable(with, without);
    }

    // --- data warnings, from a predicate ---

    // Every discovered file's name ends in ".mp3", so file::name AS NUMBER is never a number. The
    // clean run is therefore the same predicate over a collection where the junk file has been
    // renamed to one that the predicate settles before it ever converts the name. The junk file's
    // predicate is still answered, false, by the last operand, so only data warnings are in play.
    private const string WarnsForJunkOnly =
        @"file::name == ""a.mp3"" OR (file::name == ""junk.mp3"" AND file::name AS NUMBER > 1 AND file::size < 0)";

    [Test]
    public async Task NoWarnData_OverDataThatWarns_IsIndistinguishableFromTheSameRunOverCleanData()
    {
        _collection.Mp3("a.mp3");
        var junk = _collection.Mp3("junk.mp3");

        var warning = await RunAsync("list", "--strict-exit-code", $"--filter={WarnsForJunkOnly}", _collection.Root);
        var suppressed = await RunAsync("list", "--strict-exit-code", "--no-warn=data", $"--filter={WarnsForJunkOnly}", _collection.Root);
        File.Move(junk, _collection.PathOf("clean.mp3"));
        var clean = await RunAsync("list", "--strict-exit-code", $"--filter={WarnsForJunkOnly}", _collection.Root);

        Assert.That(warning.ExitCode, Is.EqualTo(10), "the scenario must actually warn");
        Assert.That(warning.StdErr, Does.Contain(junk));
        AssertIndistinguishable(suppressed, clean);
        Assert.That(clean.ExitCode, Is.EqualTo(0));
    }

    // The conversion is consumed, and warns, before the false right operand settles the AND.
    [Test]
    public async Task NoWarnData_WhereADataWarningWouldHaveFiredAndNothingMatched_Returns20()
    {
        _collection.Mp3("track.mp3");
        const string predicate = "--filter=file::name AS NUMBER > 1 AND file::size < 0";

        var warning = await RunAsync("list", "--strict-exit-code", predicate, _collection.Root);
        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--no-warn=data", predicate, _collection.Root);

        Assert.That(warning.ExitCode, Is.EqualTo(10), "the scenario must actually warn");
        Assert.That(exitCode, Is.EqualTo(20));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Is.Empty);
    }

    // Suppressing data warnings does not hide a file whose answer the command decided.
    [Test]
    public async Task NoWarnData_WhereTheOnlyFilesPredicateWasUnusable_Returns11_WithOnlyTheUnansweredWarning()
    {
        _collection.Mp3("track.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--no-warn=data", "--filter=file::name AS NUMBER > 1", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(11));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr.Trim(), Is.EqualTo("goro: warning: the predicate could not be answered for the one file examined, which was not listed"));
    }

    [Test]
    public async Task NoWarnUnanswered_WhereAPredicateWasUnusable_Returns10_WithOnlyTheDataWarning()
    {
        var track = _collection.Mp3("track.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--no-warn=unanswered", "--filter=file::name AS NUMBER > 1", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(10));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr.Trim(), Is.EqualTo($"goro: warning: {track}: cannot interpret the data of file::name AS NUMBER"));
    }

    [Test]
    public async Task NoWarnDataAndUnanswered_WhereTheOnlyFilesPredicateWasUnusable_Returns20_Silently()
    {
        _collection.Mp3("track.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--no-warn=data,unanswered", "--filter=file::name AS NUMBER > 1", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(20));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Is.Empty);
    }

    // The good file's name warns; the unreadable one warns that it could not be read.
    private const string WarnsBothWays = "--filter=file::name AS NUMBER > 1 OR file::duration >= 0";

    [Test]
    public async Task NoWarnData_WithAFilter_WhereAFileAlsoCouldNotBeRead_Still12_WithOnlyTheFileWarning()
    {
        var good = _collection.Mp3("good.mp3");
        var bad = _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--no-warn=data", WarnsBothWays, _collection.Root);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(stdOut.Trim(), Is.EqualTo(good));
        Assert.That(stdErr.Trim(), Does.StartWith($"goro: warning: {bad}: cannot be read: ").And.Not.Contain("\n"));
    }

    [Test]
    public async Task NoWarnFile_WhereAFileCouldNotBeReadAndADataWarningFired_Returns10_WithOnlyTheDataWarning()
    {
        var good = _collection.Mp3("good.mp3");
        _collection.NotAudio("bad.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--no-warn=file", WarnsBothWays, _collection.Root);

        Assert.That(exitCode, Is.EqualTo(10));
        Assert.That(stdOut.Trim(), Is.EqualTo(good));
        Assert.That(stdErr.Trim(), Is.EqualTo($"goro: warning: {good}: cannot interpret the data of file::name AS NUMBER"));
    }

    // The good file is answered by its duration and name; the other readable one is left without an
    // answer; the unreadable one cannot have its duration read.
    private const string WarnsThreeWays = @"--filter=file::name AS NUMBER > 1 OR (file::duration >= 0 AND file::name == ""good.mp3"")";

    [Test]
    public async Task NoWarn_AllAndEveryCategory_WithAFilterThatWarnsThreeWays_AreIdenticalInEveryChannel()
    {
        _collection.Mp3("good.mp3");
        _collection.Mp3("other.mp3");
        _collection.NotAudio("bad.mp3");

        var warning = await RunAsync("list", "--strict-exit-code", WarnsThreeWays, _collection.Root);
        var all = await RunAsync("list", "--strict-exit-code", "--no-warn=all", WarnsThreeWays, _collection.Root);
        var separate = await RunAsync("list", "--strict-exit-code", "--no-warn", "all", WarnsThreeWays, _collection.Root);
        var every = await RunAsync("list", "--strict-exit-code", "--no-warn=data,unanswered,incomplete,file", WarnsThreeWays, _collection.Root);

        Assert.That(warning.ExitCode, Is.EqualTo(13), "the scenario must actually warn");
        Assert.That(warning.StdErr, Does.Contain("could not be answered for 1 of the 2 files examined"));
        Assert.That(all.ExitCode, Is.EqualTo(0));
        Assert.That(all.StdErr, Is.Empty);
        AssertIndistinguishable(separate, all);
        AssertIndistinguishable(every, all);
    }

    [Test]
    public async Task NoWarnData_WithAFilterThatMeetsNoUnusableData_ChangesNothing()
    {
        _collection.Mp3("a.mp3");
        _collection.Mp3("b.mp3");
        _collection.NotAudio("bad.mp3");
        const string predicate = @"--filter=file::name == ""a.mp3"" OR file::duration > 100000";

        var without = await RunAsync("list", "--strict-exit-code", predicate, _collection.Root);
        var with = await RunAsync("list", "--strict-exit-code", "--no-warn=data", predicate, _collection.Root);

        Assert.That(without.ExitCode, Is.EqualTo(13));
        AssertIndistinguishable(with, without);
    }

    // --- rejected command lines ---

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

    // A bare --no-warn would suppress every category, the warnings about unreadable files among
    // them, so it needs its category said: at the end of the line it has none, and before a
    // pathspec or another option it takes that for its category and rejects it.
    [TestCase("list", "end")]
    [TestCase("list", "pathspec")]
    [TestCase("hash", "end")]
    [TestCase("hash", "pathspec")]
    [TestCase("hash", "option")]
    public async Task NoWarn_WithoutACategory_IsRejectedWith2BeforeAnythingIsProcessed(string command, string followedBy)
    {
        _collection.NotAudio("bad.mp3");
        string[] args = followedBy switch
        {
            "end" => [command, _collection.Root, "--no-warn"],
            "pathspec" => [command, "--no-warn", _collection.Root],
            _ => [command, "--no-warn", "--strict-exit-code", _collection.Root],
        };

        var (exitCode, stdOut, stdErr) = await RunAsync(args);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Does.Contain(followedBy == "pathspec" ? "No warning category" : "needs its categories"));
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

    // --no-warn takes its categories as any option takes its value, attached or separate, so the
    // argument after it is the category list and the pathspec comes after that.
    [Test]
    public async Task NoWarn_WithSeparateValue_FollowedByAPathspec()
    {
        var file = _collection.Mp3("a.mp3");

        var (exitCode, stdOut, _) = await RunAsync("list", "--no-warn", "data", _collection.Root);

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
