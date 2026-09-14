using Goro.Discovery;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Discovery;

public class FileDiscoveryServiceTests
{
    private DirectoryInfo _tempDir = null!;
    private FileDiscoveryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Directory.CreateTempSubdirectory("goro-tests-");
        _service = new FileDiscoveryService();
    }

    [TearDown]
    public void TearDown()
    {
        _tempDir.Delete(recursive: true);
    }

    [Test]
    public async Task DiscoverAsync_LiteralMp3File_YieldsThatFile()
    {
        var file = Path.Combine(_tempDir.FullName, "track.mp3");
        File.WriteAllText(file, "audio");

        var results = await _service.DiscoverAsync([file], CancellationToken.None).ToListAsync();

        Assert.That(results, Is.EqualTo(new[] { Path.GetFullPath(file) }));
    }

    [Test]
    public async Task DiscoverAsync_LiteralNonMp3File_YieldsNothing()
    {
        var file = Path.Combine(_tempDir.FullName, "notes.txt");
        File.WriteAllText(file, "not audio");

        var results = await _service.DiscoverAsync([file], CancellationToken.None).ToListAsync();

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task DiscoverAsync_Directory_YieldsOnlyMp3FilesRecursively()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir.FullName, "album"));
        var mp3A = Path.Combine(_tempDir.FullName, "a.mp3");
        var mp3B = Path.Combine(subDir.FullName, "b.mp3");
        var txt = Path.Combine(_tempDir.FullName, "cover.txt");
        File.WriteAllText(mp3A, "a");
        File.WriteAllText(mp3B, "b");
        File.WriteAllText(txt, "not audio");

        var results = await _service.DiscoverAsync([_tempDir.FullName], CancellationToken.None).ToListAsync();

        Assert.That(results, Is.EquivalentTo(new[] { Path.GetFullPath(mp3A), Path.GetFullPath(mp3B) }));
    }

    [Test]
    public void DiscoverAsync_NonexistentLiteralPath_ThrowsFileNotFoundException()
    {
        var missing = Path.Combine(_tempDir.FullName, "missing.mp3");

        Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.DiscoverAsync([missing], CancellationToken.None).ToListAsync().AsTask());
    }

    [Test]
    public async Task DiscoverAsync_GlobWithNoDirectoryPrefix_MatchesSameAsCurrentDirectory()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir.FullName, "album"));
        var mp3A = Path.Combine(_tempDir.FullName, "a.mp3");
        var mp3B = Path.Combine(subDir.FullName, "b.mp3");
        File.WriteAllText(mp3A, "a");
        File.WriteAllText(mp3B, "b");

        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir.FullName);

            // Re-derive expected paths from the OS-canonicalized current directory
            // (e.g. macOS resolves /tmp-style symlinks like /var -> /private/var only
            // once you cd into it), rather than from _tempDir.FullName directly, so the
            // comparison isn't sensitive to that platform quirk.
            var canonicalBase = Directory.GetCurrentDirectory();

            var globResults = await _service.DiscoverAsync(["*.mp3"], CancellationToken.None).ToListAsync();
            var dotResults = await _service.DiscoverAsync(["."], CancellationToken.None).ToListAsync();

            Assert.That(globResults, Is.EquivalentTo(dotResults));
            Assert.That(globResults, Is.EquivalentTo(new[]
            {
                Path.Combine(canonicalBase, "a.mp3"),
                Path.Combine(canonicalBase, "album", "b.mp3"),
            }));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Test]
    public async Task DiscoverAsync_GlobWithDirectoryPrefix_MatchesRecursivelyRootedAtThatDirectory()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir.FullName, "album"));
        var nestedDir = Directory.CreateDirectory(Path.Combine(subDir.FullName, "disc1"));
        var mp3InSub = Path.Combine(subDir.FullName, "b.mp3");
        var mp3Nested = Path.Combine(nestedDir.FullName, "c.mp3");
        var mp3Outside = Path.Combine(_tempDir.FullName, "a.mp3");
        File.WriteAllText(mp3InSub, "b");
        File.WriteAllText(mp3Nested, "c");
        File.WriteAllText(mp3Outside, "a");

        var pattern = Path.Combine(subDir.FullName, "*.mp3");
        var results = await _service.DiscoverAsync([pattern], CancellationToken.None).ToListAsync();

        Assert.That(results, Is.EquivalentTo(new[] { Path.GetFullPath(mp3InSub), Path.GetFullPath(mp3Nested) }));
    }
}
