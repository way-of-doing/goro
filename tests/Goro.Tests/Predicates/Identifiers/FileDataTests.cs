using Goro.Predicates.Identifiers;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Predicates.Identifiers;

public class FileDataTests
{
    private const string FilePath = "/music/01 Intro.mp3";

    [Test]
    public void Get_LoadsTheFacetForTheFilesPath()
    {
        var facet = new CountingFacet<string>(path => $"loaded {path}");

        Assert.That(TestFiles.Data(FilePath).Get(facet), Is.EqualTo($"loaded {FilePath}"));
    }

    [Test]
    public void Get_SameFacetTwice_LoadsItOnce()
    {
        var facet = new CountingFacet<object>(_ => new object());
        var file = TestFiles.Data(FilePath);

        var first = file.Get(facet);
        var second = file.Get(facet);

        Assert.That(second, Is.SameAs(first));
        Assert.That(facet.Loads, Is.EqualTo(1));
    }

    [Test]
    public void Get_OneFacet_DoesNotLoadAnother()
    {
        var asked = new CountingFacet<int>(_ => 1);
        var notAsked = new CountingFacet<int>(_ => 2);

        TestFiles.Data(FilePath).Get(asked);

        Assert.That(notAsked.Loads, Is.Zero);
    }

    [Test]
    public void Get_SameFacetForTwoFiles_LoadsItForEach()
    {
        var facet = new CountingFacet<string>(path => path);

        TestFiles.Data("/music/a.mp3").Get(facet);
        TestFiles.Data("/music/b.mp3").Get(facet);

        Assert.That(facet.Loads, Is.EqualTo(2));
    }

    [Test]
    public void Get_FacetThatFails_ThrowsUnreadableFileExceptionCarryingTheCause()
    {
        var cause = new IOException("the disk is on fire");
        var facet = new CountingFacet<int>(_ => throw cause);

        var exception = Assert.Throws<UnreadableFileException>(() => TestFiles.Data(FilePath).Get(facet));

        Assert.That(exception.Path, Is.EqualTo(FilePath));
        Assert.That(exception.Reason, Is.EqualTo("the disk is on fire"));
        Assert.That(exception.InnerException, Is.SameAs(cause));
    }

    [Test]
    public void Get_FacetThatFailed_IsNotLoadedAgainAndFailsTheSameWay()
    {
        var facet = new CountingFacet<int>(_ => throw new IOException("gone"));
        var file = TestFiles.Data(FilePath);

        var first = Assert.Throws<UnreadableFileException>(() => file.Get(facet));
        var second = Assert.Throws<UnreadableFileException>(() => file.Get(facet));

        Assert.That(second, Is.SameAs(first));
        Assert.That(facet.Loads, Is.EqualTo(1));
    }

    [Test]
    public void Get_FacetThatFailed_DoesNotStopAnotherLoading()
    {
        var failing = new CountingFacet<int>(_ => throw new IOException("damaged tags"));
        var working = new CountingFacet<int>(_ => 42);
        var file = TestFiles.Data(FilePath);

        Assert.Throws<UnreadableFileException>(() => file.Get(failing));

        Assert.That(file.Get(working), Is.EqualTo(42));
    }

    [Test]
    public void Get_FacetThrowingUnreadableFileException_IsNotWrappedAgain()
    {
        var thrown = new UnreadableFileException(FilePath, "already explained");
        var facet = new CountingFacet<int>(_ => throw thrown);

        var exception = Assert.Throws<UnreadableFileException>(() => TestFiles.Data(FilePath).Get(facet));

        Assert.That(exception, Is.SameAs(thrown));
    }

    private sealed class CountingFacet<T>(Func<string, T> load) : FileFacet<T> where T : notnull
    {
        public int Loads { get; private set; }

        public override T Load(FileData file)
        {
            Loads++;
            return load(file.Path);
        }
    }
}
