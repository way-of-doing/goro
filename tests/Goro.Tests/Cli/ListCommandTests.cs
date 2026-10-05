using System.Text.Json;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Cli;

public class ListCommandTests
{
    private DirectoryInfo _tempDir = null!;

    [SetUp]
    public void SetUp() => _tempDir = Directory.CreateTempSubdirectory("goro-cli-list-tests-");

    [TearDown]
    public void TearDown() => _tempDir.Delete(recursive: true);

    private string WriteMp3(string name)
    {
        var path = Path.Combine(_tempDir.FullName, name);
        File.WriteAllBytes(path, SyntheticMp3Builder.BuildMp3(SyntheticMp3Builder.BuildAudioFrames()));
        return path;
    }

    [Test]
    public async Task List_DefaultOutput_IsPlainOnePathPerLine()
    {
        var file = WriteMp3("track.mp3");
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, _) = await app.RunCapturedAsync("list", _tempDir.FullName);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut.Trim(), Is.EqualTo(Path.GetFullPath(file)));
    }

    [Test]
    public async Task List_OutputOptionIsCaseInsensitive_AndProducesValidJson()
    {
        var file = WriteMp3("track.mp3");
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, _) = await app.RunCapturedAsync("list", "-o", "JSON", _tempDir.FullName);

        Assert.That(exitCode, Is.EqualTo(0));
        using var document = JsonDocument.Parse(stdOut);
        var files = document.RootElement.EnumerateArray().Select(e => e.GetProperty("file").GetString()).ToList();
        Assert.That(files, Is.EqualTo(new[] { Path.GetFullPath(file) }));
    }

    [Test]
    public async Task List_InvalidOutputValue_IsRejectedWith2()
    {
        var app = GoroAppFactory.Create();

        var (exitCode, _, _) = await app.RunCapturedAsync("list", "-o", "yaml", _tempDir.FullName);

        Assert.That(exitCode, Is.EqualTo(2));
    }

    [Test]
    public async Task List_NoPathspecs_DefaultsToCurrentDirectory()
    {
        var file = WriteMp3("track.mp3");
        var app = GoroAppFactory.Create();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_tempDir.FullName);

            // Re-derive the expected path from the OS-canonicalized current directory
            // (e.g. macOS resolves /tmp-style symlinks only once you cd into them)
            // rather than from _tempDir.FullName directly.
            var expected = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(file));

            var (exitCode, stdOut, _) = await app.RunCapturedAsync("list");

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdOut.Trim(), Is.EqualTo(expected));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Test]
    public async Task List_DoubleDashSeparator_AllowsADashPrefixedPathspec()
    {
        var file = WriteMp3("-dashprefixed.mp3");
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, _) = await app.RunCapturedAsync("list", "--", file);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut.Trim(), Is.EqualTo(Path.GetFullPath(file)));
    }

    [Test]
    public async Task List_MissingPathspecAfterAValidDirectory_IsRejectedWithNoOutput()
    {
        WriteMp3("track.mp3");
        var missing = Path.Combine(_tempDir.FullName, "missing.mp3");
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, stdErr) = await app.RunCapturedAsync("list", _tempDir.FullName, missing);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Does.StartWith("goro: error: ").And.Contain(missing));
    }

    [Test]
    public async Task List_GlobBreakingTheGlobRules_IsRejectedWithNoOutput()
    {
        WriteMp3("track.mp3");
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, _) = await app.RunCapturedAsync("list", Path.Combine(_tempDir.FullName, "*a*.mp3"));

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdOut, Is.Empty);
    }

    [Test]
    public async Task List_OnlyANonMp3FileNamed_CompletesWithNothingListed()
    {
        var cover = Path.Combine(_tempDir.FullName, "cover.jpg");
        File.WriteAllText(cover, "not audio");
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, stdErr) = await app.RunCapturedAsync("list", cover);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Is.Empty);
    }
}
