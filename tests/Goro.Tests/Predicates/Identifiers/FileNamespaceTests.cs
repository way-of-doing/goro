using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Predicates.Identifiers;

/// <summary>
/// The identifiers of the <c>file</c> namespace, resolved through the built-in catalog as the
/// evaluator resolves them.
/// </summary>
public class FileNamespaceTests
{
    private DirectoryInfo _tempDir = null!;

    [SetUp]
    public void SetUp() => _tempDir = Directory.CreateTempSubdirectory("goro-tests-");

    [TearDown]
    public void TearDown() => _tempDir.Delete(recursive: true);

    private string InTemp(params string[] relativePath) => Path.Combine([_tempDir.FullName, .. relativePath]);

    private string WriteFile(byte[] contents, params string[] relativePath)
    {
        var path = InTemp(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static Value<T> Resolve<T>(string identifier, FileData file) where T : notnull
    {
        var lookup = BuiltInCatalog.Instance.Lookup(new IdentifierName(["file"], identifier));
        var declaration = (IdentifierDeclaration<T>)((IdentifierLookup.Found)lookup).Declaration;
        return declaration.Binding.Resolve(file, new Origin(new SourceId(0), $"file::{identifier}", 0));
    }

    private static Value<T> Resolve<T>(string identifier, string path) where T : notnull =>
        Resolve<T>(identifier, new FileData(path));

    private static T SingleDatum<T>(Value<T> value) where T : notnull
    {
        Assert.That(value.Occurrences, Has.Length.EqualTo(1).And.All.InstanceOf<Usable<T>>(), value.ToString());
        return ((Usable<T>)value.Occurrences[0]).Datum;
    }

    // Nothing is created on disk for these: the extension needs nothing read.
    [TestCase("a.flac", "flac")]
    [TestCase("a.tar.gz", "gz")]
    [TestCase("TRACK.MP3", "MP3")]
    [TestCase(".hidden", null)]
    [TestCase("README", null)]
    [TestCase("trailing.", null)]
    public void Extension_IsWhatFollowsTheLastDot_OrAbsent(string fileName, string? expected)
    {
        var extension = Resolve<string>("extension", InTemp("missing", fileName));

        if (expected is null)
        {
            Assert.That(extension.IsAbsent, Is.True, extension.ToString());
        }
        else
        {
            Assert.That(SingleDatum(extension), Is.EqualTo(expected));
        }
    }

    [Test]
    public void PathNameAndExtension_FileThatDoesNotExist_ResolveWithoutReadingAnything()
    {
        var path = InTemp("no such directory", "01 Intro.mp3");

        Assert.Multiple(() =>
        {
            Assert.That(SingleDatum(Resolve<string>("path", path)), Is.EqualTo(FilePaths.ToPredicatePath(path)));
            Assert.That(SingleDatum(Resolve<string>("name", path)), Is.EqualTo("01 Intro.mp3"));
            Assert.That(SingleDatum(Resolve<string>("extension", path)), Is.EqualTo("mp3"));
        });
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void Path_OnUnix_IsThePathAsDiscovered()
    {
        var path = InTemp("Metallica", "01 Intro.mp3");

        Assert.That(SingleDatum(Resolve<string>("path", path)), Is.EqualTo(path));
    }

    [TestCase(@"C:\Music\01 Intro.flac", '\\', "C:/Music/01 Intro.flac")]
    [TestCase(@"\\server\share\Music\a.mp3", '\\', "//server/share/Music/a.mp3")]
    [TestCase(@"C:\Music/Albums\a.mp3", '\\', "C:/Music/Albums/a.mp3")]
    [TestCase(@"/music/AC\DC/a.mp3", '/', @"/music/AC\DC/a.mp3")]
    public void ToPredicatePath_WritesTheSeparatorAsSlash_AndNothingElse(string path, char separator, string expected)
    {
        Assert.That(FilePaths.ToPredicatePath(path, separator), Is.EqualTo(expected));
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void PathAndName_OnUnix_KeepABackslashAsPartOfTheName()
    {
        var path = WriteFile([], @"AC\DC - Thunderstruck.mp3");

        Assert.That(SingleDatum(Resolve<string>("path", path)), Does.EndWith(@"/AC\DC - Thunderstruck.mp3"));
        Assert.That(SingleDatum(Resolve<string>("name", path)), Is.EqualTo(@"AC\DC - Thunderstruck.mp3"));
    }

    [Test]
    public void PathNameAndSize_ThroughASymbolicLink_AreTheLinksPathAndNameAndTheTargetsSize()
    {
        var target = WriteFile(new byte[1234], "library", "real track.mp3");
        var link = CreateSymbolicLink(InTemp("links", "alias.mp3"), target);

        Assert.Multiple(() =>
        {
            Assert.That(SingleDatum(Resolve<string>("path", link)), Is.EqualTo(FilePaths.ToPredicatePath(link)));
            Assert.That(SingleDatum(Resolve<string>("name", link)), Is.EqualTo("alias.mp3"));
            Assert.That(SingleDatum(Resolve<ByteCount>("size", link)), Is.EqualTo(new ByteCount(1234)));
        });
    }

    [Test]
    public void Size_OfABrokenSymbolicLink_MakesTheFileUnreadable()
    {
        var link = CreateSymbolicLink(InTemp("links", "alias.mp3"), InTemp("library", "gone.mp3"));

        Assert.Throws<UnreadableFileException>(() => Resolve<ByteCount>("size", link));
        Assert.That(SingleDatum(Resolve<string>("name", link)), Is.EqualTo("alias.mp3"));
    }

    [Test]
    public void Size_IsTheFilesLengthInBytes()
    {
        var path = WriteFile(new byte[5000], "track.mp3");

        Assert.That(SingleDatum(Resolve<ByteCount>("size", path)), Is.EqualTo(new ByteCount(5000)));
    }

    [Test]
    public void Size_FileThatDoesNotExist_MakesTheFileUnreadable()
    {
        var path = InTemp("gone.mp3");

        var exception = Assert.Throws<UnreadableFileException>(() => Resolve<ByteCount>("size", path));

        Assert.That(exception.Path, Is.EqualTo(path));
    }

    [Test]
    [Platform(Exclude = "Win")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void Size_FileThatMayNotBeRead_IsStillKnown()
    {
        var path = WriteFile(new byte[300], "locked.mp3");
        File.SetUnixFileMode(path, UnixFileMode.None);
        try
        {
            Assert.That(SingleDatum(Resolve<ByteCount>("size", path)), Is.EqualTo(new ByteCount(300)));
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Test]
    public void Duration_OfAnMp3_IsTruncatedToWholeSeconds()
    {
        // 180 frames of 1152 samples at 44.1kHz is about 4.7 seconds: truncated, not rounded.
        var path = WriteFile(SyntheticMp3Builder.BuildMp3(SyntheticMp3Builder.BuildAudioFrames(180)), "track.mp3");

        Assert.That(SingleDatum(Resolve<Duration>("duration", path)), Is.EqualTo(new Duration(4)));
    }

    [Test]
    public void Duration_OfAnMp3WithADamagedId3v2Tag_StillResolves()
    {
        // A TIT2 frame claiming a size far beyond the tag. warnings.md: a file is unreadable for
        // file::duration when its audio properties cannot be parsed, not when its tags cannot.
        // TagLibSharp parses the tags on the way to the audio, so this guards its tolerance.
        var tag = SyntheticMp3Builder.BuildId3V2(64);
        "TIT2"u8.CopyTo(tag.AsSpan(10));
        tag[14] = 0x7F;
        tag[15] = 0xFF;
        var path = WriteFile(SyntheticMp3Builder.BuildMp3(SyntheticMp3Builder.BuildAudioFrames(180), id3v2: tag), "track.mp3");

        Assert.That(SingleDatum(Resolve<Duration>("duration", path)), Is.EqualTo(new Duration(4)));
    }

    [Test]
    public void Duration_OfAFileThatIsNotAudio_MakesTheFileUnreadable()
    {
        var path = WriteFile("this is a shopping list, not a song"u8.ToArray(), "list.mp3");

        var exception = Assert.Throws<UnreadableFileException>(() => Resolve<Duration>("duration", path));

        Assert.That(exception.Path, Is.EqualTo(path));
    }

    [Test]
    public void Duration_OfNoiseNamedMp3_MakesTheFileUnreadable()
    {
        var noise = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
        var path = WriteFile(noise, "noise.mp3");

        Assert.Throws<UnreadableFileException>(() => Resolve<Duration>("duration", path));
    }

    [Test]
    public void SizeAndDuration_ShareTheFilesData_AndFailIndependently()
    {
        var file = new FileData(WriteFile("not audio"u8.ToArray(), "list.mp3"));

        Assert.Throws<UnreadableFileException>(() => Resolve<Duration>("duration", file));

        Assert.That(SingleDatum(Resolve<ByteCount>("size", file)), Is.EqualTo(new ByteCount(9)));
    }

    private static string CreateSymbolicLink(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Ignore($"Symbolic links cannot be created here: {ex.Message}");
        }

        return link;
    }
}
