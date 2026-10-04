using Goro.Discovery;

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

    private Task<List<string>> DiscoverAsync(params string[] pathSpecs) =>
        _service.DiscoverAsync(_service.Resolve(pathSpecs), CancellationToken.None).ToListAsync().AsTask();

    private string WriteFile(params string[] relativePath)
    {
        var path = Path.Combine([_tempDir.FullName, .. relativePath]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "audio");
        return Path.GetFullPath(path);
    }

    private string InTemp(params string[] relativePath) => Path.Combine([_tempDir.FullName, .. relativePath]);

    [Test]
    public async Task DiscoverAsync_LiteralMp3File_YieldsThatFile()
    {
        var file = WriteFile("track.mp3");

        var results = await DiscoverAsync(file);

        Assert.That(results, Is.EqualTo(new[] { file }));
    }

    [Test]
    public async Task DiscoverAsync_LiteralNonMp3File_YieldsNothing()
    {
        var file = WriteFile("notes.txt");

        var results = await DiscoverAsync(file);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task DiscoverAsync_Directory_YieldsOnlyMp3FilesRecursively()
    {
        var mp3A = WriteFile("a.mp3");
        var mp3B = WriteFile("album", "b.mp3");
        WriteFile("cover.txt");

        var results = await DiscoverAsync(_tempDir.FullName);

        Assert.That(results, Is.EquivalentTo(new[] { mp3A, mp3B }));
    }

    [Test]
    public async Task DiscoverAsync_ExtensionInAnyCase_IsACandidate()
    {
        var lower = WriteFile("a.mp3");
        var upper = WriteFile("b.MP3");
        var mixed = WriteFile("c.Mp3");

        var results = await DiscoverAsync(_tempDir.FullName);

        Assert.That(results, Is.EquivalentTo(new[] { lower, upper, mixed }));
    }

    [Test]
    public async Task DiscoverAsync_NamesWithoutTheMp3Extension_AreNotCandidates()
    {
        // ".mp3" has no extension at all, as for file::extension; the others end in something else.
        var dotFile = WriteFile(".mp3");
        var backup = WriteFile("track.mp3.bak");
        var trailing = WriteFile("track.mp3.");

        var results = await DiscoverAsync(_tempDir.FullName, dotFile, backup, trailing);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Resolve_NonexistentLiteralPath_Throws()
    {
        var missing = InTemp("missing.mp3");

        var ex = Assert.Throws<PathSpecException>(() => _service.Resolve([missing]));
        Assert.That(ex.PathSpec, Is.EqualTo(missing));
    }

    [Test]
    public void Resolve_NonexistentPathAfterAValidDirectory_ThrowsBeforeAnythingIsDiscovered()
    {
        WriteFile("a.mp3");

        Assert.Throws<PathSpecException>(() => _service.Resolve([_tempDir.FullName, InTemp("missing.mp3")]));
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void Resolve_DirectoryThatCannotBeListed_Throws()
    {
        var locked = Directory.CreateDirectory(InTemp("locked"));
        File.SetUnixFileMode(locked.FullName, UnixFileMode.None);
        try
        {
            Assume.That(() => Directory.EnumerateFileSystemEntries(locked.FullName).Any(), Throws.Exception,
                "permissions are not enforced for this user");

            Assert.Throws<PathSpecException>(() => _service.Resolve([locked.FullName]));
        }
        finally
        {
            File.SetUnixFileMode(locked.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Test]
    public async Task DiscoverAsync_GlobWithNoDirectoryPrefix_MatchesInTheCurrentDirectoryOnly()
    {
        WriteFile("a.mp3");
        WriteFile("album", "b.mp3");

        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir.FullName);

            // Re-derive expected paths from the OS-canonicalized current directory
            // (e.g. macOS resolves /tmp-style symlinks like /var -> /private/var only
            // once you cd into it), rather than from _tempDir.FullName directly, so the
            // comparison isn't sensitive to that platform quirk.
            var canonicalBase = Directory.GetCurrentDirectory();

            var results = await DiscoverAsync("*.mp3");

            Assert.That(results, Is.EqualTo(new[] { Path.Combine(canonicalBase, "a.mp3") }));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Test]
    public async Task DiscoverAsync_GlobWithDirectoryPrefix_MatchesTheEntriesOfThatDirectoryOnly()
    {
        var mp3InAlbum = WriteFile("album", "b.mp3");
        WriteFile("album", "disc1", "c.mp3");
        WriteFile("a.mp3");

        var results = await DiscoverAsync(InTemp("album", "*.mp3"));

        Assert.That(results, Is.EqualTo(new[] { mp3InAlbum }));
    }

    [Test]
    public async Task DiscoverAsync_GlobMatchingADirectory_WalksItInFull()
    {
        var top = WriteFile("Abba", "x.mp3");
        var nested = WriteFile("Abba", "Arrival", "y.mp3");
        WriteFile("Beatles", "z.mp3");

        var results = await DiscoverAsync(InTemp("Ab*"));

        Assert.That(results, Is.EquivalentTo(new[] { top, nested }));
    }

    [Test]
    public async Task DiscoverAsync_GlobMatchingNonMp3Files_PassesThemOver()
    {
        var mp3 = WriteFile("cover.mp3");
        WriteFile("cover.jpg");

        var results = await DiscoverAsync(InTemp("cover.*"));

        Assert.That(results, Is.EqualTo(new[] { mp3 }));
    }

    [Test]
    public async Task DiscoverAsync_GlobInADirectoryThatDoesNotExist_YieldsNothing()
    {
        var results = await DiscoverAsync(InTemp("missing", "*.mp3"));

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Resolve_GlobWithTwoStars_Throws()
    {
        var pattern = InTemp("*a*.mp3");

        var ex = Assert.Throws<PathSpecException>(() => _service.Resolve([pattern]));
        Assert.That(ex.PathSpec, Is.EqualTo(pattern));
    }

    [TestCase("*", "c.mp3")]
    [TestCase("alb?m", "*.mp3")]
    [TestCase("alb?m", "c.mp3")]
    public void Resolve_GlobWithAWildcardInADirectoryComponent_Throws(string directory, string name)
    {
        WriteFile("album", "c.mp3");

        Assert.Throws<PathSpecException>(() => _service.Resolve([InTemp(directory, name)]));
    }

    [Test]
    public async Task DiscoverAsync_SameDirectoryGivenTwice_YieldsEachFileOnce()
    {
        var mp3A = WriteFile("a.mp3");

        var results = await DiscoverAsync(_tempDir.FullName, _tempDir.FullName);

        Assert.That(results, Is.EqualTo(new[] { mp3A }));
    }

    [Test]
    public async Task DiscoverAsync_SameLiteralFileGivenTwice_YieldsItOnce()
    {
        var file = WriteFile("track.mp3");

        var results = await DiscoverAsync(file, file);

        Assert.That(results, Is.EqualTo(new[] { file }));
    }

    [Test]
    public async Task DiscoverAsync_OverlappingDirectories_YieldsFilesInSubdirectoryOnce()
    {
        var mp3A = WriteFile("a.mp3");
        var mp3B = WriteFile("album", "b.mp3");

        var results = await DiscoverAsync(_tempDir.FullName, InTemp("album"));

        Assert.That(results, Is.EquivalentTo(new[] { mp3A, mp3B }));
    }

    [Test]
    public async Task DiscoverAsync_GlobAndLiteralMatchingSameFile_YieldsItOnce()
    {
        var mp3A = WriteFile("a.mp3");

        var results = await DiscoverAsync(InTemp("*.mp3"), mp3A);

        Assert.That(results, Is.EqualTo(new[] { mp3A }));
    }
}
