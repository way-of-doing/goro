using System.Text.Json;
using System.Text.RegularExpressions;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Cli;

public class HashCommandTests
{
    private DirectoryInfo _tempDir = null!;
    private string _mp3File = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Directory.CreateTempSubdirectory("goro-cli-hash-tests-");
        _mp3File = Path.Combine(_tempDir.FullName, "track.mp3");
        File.WriteAllBytes(_mp3File, SyntheticMp3Builder.BuildMp3(SyntheticMp3Builder.BuildAudioFrames(), SyntheticMp3Builder.BuildId3V2(20)));
    }

    [TearDown]
    public void TearDown() => _tempDir.Delete(recursive: true);

    [Test]
    public async Task Hash_DefaultOptions_ProducesMd5PlainLine()
    {
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, _) = await app.RunCapturedAsync("hash", _mp3File);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Regex.IsMatch(stdOut.Trim(), $@"^{Regex.Escape(Path.GetFullPath(_mp3File))} md5 [0-9a-f]{{32}}$"), Is.True, stdOut);
    }

    [Test]
    public async Task Hash_AlgorithmAndOutputOptionsAreCaseInsensitive_AndProduceSha1Json()
    {
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, _) = await app.RunCapturedAsync("hash", "-a", "SHA1", "-o", "JSON", _mp3File);

        Assert.That(exitCode, Is.EqualTo(0));
        using var document = JsonDocument.Parse(stdOut);
        var entry = document.RootElement.EnumerateArray().Single();
        Assert.That(entry.GetProperty("file").GetString(), Is.EqualTo(Path.GetFullPath(_mp3File)));
        Assert.That(entry.GetProperty("algo").GetString(), Is.EqualTo("sha1"));
        Assert.That(entry.GetProperty("hash").GetString(), Has.Length.EqualTo(40));
    }

    [Test]
    public async Task Hash_InvalidAlgorithmValue_IsRejectedWith2()
    {
        var app = GoroAppFactory.Create();

        var (exitCode, _, stdErr) = await app.RunCapturedAsync("hash", "-a", "md6", _mp3File);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdErr.TrimEnd(), Is.EqualTo("goro: error: No algorithm `md6` — pick from md5, sha1, goro!"));
    }

    [TestCase("plain")]
    [TestCase("json")]
    public async Task Hash_MissingPathspecAfterAValidFile_IsRejectedWithNoOutput(string output)
    {
        var missing = Path.Combine(_tempDir.FullName, "missing.mp3");
        var app = GoroAppFactory.Create();

        var (exitCode, stdOut, stdErr) = await app.RunCapturedAsync("hash", "-o", output, _mp3File, missing);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Does.Contain(missing));
    }
}
