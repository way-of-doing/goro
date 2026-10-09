using Goro.Domain;
using Goro.Pipeline;
using Goro.Predicates;
using Goro.Predicates.Identifiers;
using Goro.Tests.TestSupport;
using Goro.Warnings;

namespace Goro.Tests.Pipeline;

/// <summary>
/// What <c>goro list --filter</c> makes of the files of the corpus, read through Goro's reader: how
/// much of each file was read, which the <c>incomplete</c> warning counts, and which files cannot be
/// read at all (docs/concepts/warnings.md).
/// </summary>
public class PredicateStageReadingTests
{
    private static CompiledPredicate Compile(string text)
    {
        var compiled = PredicateCompiler.Compile(text, BuiltInCatalog.Instance);
        Assert.That(compiled.Succeeded, Is.True, text);
        return compiled.Value!;
    }

    private static Task<FileOutcome<ListResult>> Run(string predicate, string file) =>
        new PredicateStage(Compile(predicate)).ExecuteAsync(AudioCorpus.PathOf(file), CancellationToken.None);

    [Test]
    public async Task ATagPredicate_OnAHealthyFile_ReadsIt()
    {
        var outcome = await Run("""id3v2::field("TIT2") == "Title v2.2" """, "mp3/id3v22.mp3");

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Matched));
        Assert.That(outcome.Reading, Is.EqualTo(FileReading.Read));
    }

    [TestCase("mp3/dmg-id3v24-frame-overrun.mp3")]
    [TestCase("mp3/dmg-id3v2-version-5.mp3")]
    [TestCase("mp3/dmg-ape-item-count-high.mp3")]
    public async Task ATagPredicate_OnAFileWhoseTagIsBroken_ReadsItInPart(string file)
    {
        var outcome = await Run("""id3v2::field("TIT2") == "x" """, file);

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unmatched));
        Assert.That(outcome.Reading, Is.EqualTo(FileReading.ReadInPart));
    }

    // A file counts once it is opened, whatever the predicate asked: the damage is in a tag the
    // predicate never named.
    [Test]
    public async Task ADurationPredicate_OnAFileWhoseTagIsBroken_StillReadsItInPart()
    {
        var outcome = await Run("file::duration >= 0", "mp3/dmg-id3v24-frame-overrun.mp3");

        Assert.That(outcome.Reading, Is.EqualTo(FileReading.ReadInPart));
    }

    [Test]
    public async Task ASizePredicate_OpensNothing()
    {
        var outcome = await Run("file::size >= 0", "mp3/dmg-id3v24-frame-overrun.mp3");

        Assert.That(outcome.Reading, Is.EqualTo(FileReading.NotOpened));
    }

    [TestCase("""id3v2::field("TIT2") == "x" """)]
    [TestCase("""vorbis::field("TITLE") == "x" """)]
    [TestCase("file::duration >= 0")]
    public async Task AFileWithNoAudio_IsUnreadable_WhicheverIdentifierAsked(string predicate)
    {
        var path = AudioCorpus.PathOf("mp3/dmg-id3v24-truncated-in-tag.mp3");

        var outcome = await new PredicateStage(Compile(predicate)).ExecuteAsync(path, CancellationToken.None);

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unreadable));
        Assert.That(outcome.Warnings, Is.EqualTo(new[] { new FileWarning(path, "no MPEG audio found") }));
    }

    [Test]
    public async Task UnusableTagData_WarnsAndLeavesThePredicateUnanswered()
    {
        var outcome = await Run("""id3v2::field("TPE1") == "Motörhead" """, "mp3/dmg-id3v24-invalid-utf8.mp3");

        Assert.That(outcome.IsUnanswered, Is.True);
        Assert.That(outcome.Warnings.OfType<DataWarning>().Single().SubExpression, Is.EqualTo("""id3v2::field("TPE1")"""));
        Assert.That(outcome.Reading, Is.EqualTo(FileReading.Read), "a value that does not decode is not damage to the tag's structure");
    }

    // A file counts once it is opened: its playing time cannot be had, and the predicate never asked.
    [Test]
    public async Task ATagPredicate_OnAFileWhosePlayingTimeCannotBeHad_ReadsItInPart()
    {
        var outcome = await Run("""id3v2::field("TIT2") IS ABSENT OR TRUE""", "mp3/vbr-notag-id3v24.mp3");

        Assert.That(outcome.Reading, Is.EqualTo(FileReading.ReadInPart));
    }

    [Test]
    public async Task AnUnusableDuration_WarnsWhenCompared_AndLeavesThePredicateUnanswered()
    {
        var outcome = await Run("file::duration >= 1", "mp3/vbr-notag-id3v24.mp3");

        Assert.That(outcome.IsUnanswered, Is.True);
        Assert.That(outcome.Warnings.OfType<DataWarning>().Single().SubExpression, Is.EqualTo("file::duration"));
    }

    [Test]
    public async Task GuardingTheDuration_PassesOverTheFileSilently()
    {
        var outcome = await Run("file::duration IS USABLE AND file::duration >= 1", "mp3/vbr-notag-id3v24.mp3");

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unmatched));
        Assert.That(outcome.IsUnanswered, Is.False);
        Assert.That(outcome.Warnings, Is.Empty);
    }
}
