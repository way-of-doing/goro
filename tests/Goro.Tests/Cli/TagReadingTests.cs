using Goro.Tests.TestSupport;
using Goro.Warnings;

namespace Goro.Tests.Cli;

/// <summary>
/// Tags read end to end, from real files of the fixture corpus: what <c>goro list --filter</c>
/// lists, and the <c>incomplete</c> warning for files whose tags are damaged
/// (docs/concepts/warnings.md).
/// </summary>
public class TagReadingTests
{
    private TempCollection _collection = null!;

    [SetUp]
    public void SetUp() => _collection = new TempCollection("goro-cli-tag-reading-tests-");

    [TearDown]
    public void TearDown() => _collection.Dispose();

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(params string[] args) =>
        GoroAppFactory.Create().RunCapturedAsync(args);

    /// <summary>A copy of a corpus fixture in the collection, under its own name.</summary>
    private string Fixture(string file)
    {
        var path = _collection.PathOf(Path.GetFileName(file));
        File.Copy(AudioCorpus.PathOf(file), path);
        return path;
    }

    [Test]
    public async Task ASourceFunction_SelectsTheFilesWhoseTagsSaySo()
    {
        var v22 = Fixture("mp3/id3v22.mp3");
        Fixture("mp3/id3v24.mp3");
        Fixture("mp3/ape2.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", """--filter=id3v2::field("TIT2") == "title v2.2" """, _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { v22 }));
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task ADescribedFrame_IsSelectedByItsDescription()
    {
        var both = Fixture("mp3/id3v24.mp3");
        Fixture("mp3/id3v23.mp3");

        var (_, stdOut, _) = await RunAsync("list", """--filter=id3v2::field("TXXX", "mood") == "wistful" """, _collection.Root);

        Assert.That(Lines(stdOut), Is.EqualTo(new[] { both }));
    }

    [Test]
    public async Task FilesWhoseTagsAreBroken_AreProcessed_AndCountedOnceAsIncomplete()
    {
        var overrun = Fixture("mp3/dmg-id3v24-frame-overrun.mp3");
        var unusable = Fixture("mp3/dmg-id3v2-version-5.mp3");
        var healthy = Fixture("mp3/id3v24.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", """--filter=id3v2::field("TPE1") IS ABSENT OR TRUE""", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(12));
        Assert.That(Lines(stdOut), Is.EquivalentTo(new[] { overrun, unusable, healthy }));
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { new IncompleteWarning(2, 3).ToString() }));
    }

    [Test]
    public async Task ABrokenTag_IsReported_ThoughThePredicateNamedNoTagAtAll()
    {
        Fixture("mp3/dmg-ape-item-overrun.mp3");

        var (exitCode, _, stdErr) = await RunAsync("list", "--strict-exit-code", "--filter=file::duration >= 0", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(12));
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { new IncompleteWarning(1, 1).ToString() }));
    }

    [Test]
    public async Task AFileWithNoAudio_IsUnreadable_AndOutranksTheIncompleteOnes()
    {
        Fixture("mp3/dmg-id3v24-frame-overrun.mp3");
        var truncated = Fixture("mp3/dmg-id3v24-truncated-in-tag.mp3");

        var (exitCode, _, stdErr) = await RunAsync("list", "--strict-exit-code", """--filter=id3v2::field("TIT2") IS ABSENT OR TRUE""", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(13));
        Assert.That(Lines(stdErr), Is.EqualTo(new[]
        {
            new FileWarning(truncated, "no MPEG audio found").ToString(),
            new IncompleteWarning(1, 1).ToString(),
        }));
    }

    [Test]
    public async Task NoWarnIncomplete_OnRealFiles_IsIndistinguishableFromHealthyOnes()
    {
        Fixture("mp3/dmg-id3v24-zeroed-block.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--no-warn=incomplete", "--filter=file::size > 0 AND file::duration >= 0", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Has.Length.EqualTo(1));
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task UnreadableTagData_WarnsAsData_NotAsIncomplete()
    {
        var file = Fixture("mp3/dmg-id3v24-invalid-utf8.mp3");

        var (exitCode, _, stdErr) = await RunAsync("list", "--strict-exit-code", """--filter=id3v2::field("TPE1") == "x" """, _collection.Root);

        Assert.That(exitCode, Is.EqualTo(11), "the predicate went unanswered, and nothing is broken");
        Assert.That(Lines(stdErr)[0], Is.EqualTo(new DataWarning(file, """id3v2::field("TPE1")""").ToString()));
        Assert.That(stdErr, Does.Not.Contain("goro audit"));
    }

    [Test]
    public async Task AConceptWrittenWithoutASource_FindsTheFilesWhicheverTagHoldsIt()
    {
        var id3v2 = Fixture("mp3/id3v24.mp3");
        var ape = Fixture("mp3/ape2.mp3");
        Fixture("mp3/id3v22.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", """--filter=artist == "motorhead" """, _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Is.EquivalentTo(new[] { id3v2, ape }), "Motörhead is one of two artists in each, and comparison ignores the accent");
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task YearAndGenre_AreInterpreted()
    {
        var v24 = Fixture("mp3/id3v24.mp3");
        var v23 = Fixture("mp3/id3v23.mp3");
        var v22 = Fixture("mp3/id3v22.mp3");

        var byYear = await RunAsync("list", "--filter=year == 1991", _collection.Root);
        var byGenre = await RunAsync("list", """--filter=genre == "post-rock" """, _collection.Root);
        var rock = await RunAsync("list", """--filter=genre == "rock" """, _collection.Root);

        Assert.That(Lines(byYear.StdOut), Is.EquivalentTo(new[] { v24, v23, v22 }));
        Assert.That(Lines(byGenre.StdOut), Is.EqualTo(new[] { v24 }), "(17)Post-Rock refines Rock");
        Assert.That(Lines(rock.StdOut), Is.EquivalentTo(new[] { v24, v23, v22 }), "(17), (17)Post-Rock and Rock/Metal");
    }

    [Test]
    public async Task ADurationPredicate_ReadsTheTrimmedPlayingTime_AndTheFaqsGuardPassesOverTheRest()
    {
        var cbr = Fixture("seeds/mp3-cbr.mp3");
        var headerless = Fixture("seeds/mp3-cbr-notag.mp3");
        Fixture("mp3/vbr-notag-id3v24.mp3");

        var (exitCode, stdOut, stdErr) = await RunAsync("list", "--strict-exit-code", "--filter=file::duration IS USABLE AND file::duration == 3s", _collection.Root);

        Assert.That(Lines(stdOut), Is.EquivalentTo(new[] { cbr, headerless }));
        Assert.That(exitCode, Is.EqualTo(12), "the file with no playing time is still counted as incomplete");
        Assert.That(Lines(stdErr), Is.EqualTo(new[] { new IncompleteWarning(1, 3).ToString() }));
    }
}
