using Goro.Predicates.Identifiers;
using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Predicates.Identifiers;

public class FileDataLoaderTests
{
    [Test]
    public void NothingIsOpened_UntilSomethingInsideTheFileIsAskedFor()
    {
        using var loader = new FileDataLoader("/no/such/file.mp3", ReadPolicy.Default);

        Assert.That(loader.Opened, Is.False);
    }

    [Test]
    public void TheLayout_IsAnalysedOnce_AndTheFileCountsAsOpened()
    {
        using var loader = new FileDataLoader(AudioCorpus.PathOf("mp3/id3v24.mp3"), ReadPolicy.Default);

        var first = loader.Layout;

        Assert.That(loader.Layout, Is.SameAs(first));
        Assert.That(loader.Opened, Is.True);
        Assert.That(first.Audio, Is.InstanceOf<AudioLocation.Found>());
    }

    [Test]
    public void Disposing_ClosesTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"goro-loader-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, AudioCorpus.Bytes("mp3/id3v24.mp3"));
        try
        {
            var loader = new FileDataLoader(path, ReadPolicy.Default);
            _ = loader.Layout;
            loader.Dispose();

            // Opened for writing with no sharing, which only succeeds once nothing holds the file open.
            Assert.DoesNotThrow(() => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AFileThatIsNotThere_ThrowsWhenItsLayoutIsAskedFor()
    {
        using var loader = new FileDataLoader("/no/such/file.mp3", ReadPolicy.Default);

        Assert.That(() => loader.Layout, Throws.InstanceOf<IOException>());
    }
}
