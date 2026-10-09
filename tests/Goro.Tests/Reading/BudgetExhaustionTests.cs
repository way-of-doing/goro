using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;
using static Goro.Tests.TestSupport.Id3v2Bytes;

namespace Goro.Tests.Reading;

/// <summary>
/// A file made to cost more than the budgets allow is read as far as they do, and reported: the
/// structure the budget cut off is broken off rather than silently short, and the file is
/// incomplete. However it is made, the analysis takes no more from it than the policy's ceiling.
/// </summary>
public class BudgetExhaustionTests
{
    private static readonly ReadPolicy Tight = new(HeadWindow: 1024, TailWindow: 1024, SniffBudget: 256, SearchBudget: 64 * 1024, StructureBudget: 4096);

    [Test]
    public void ATagOfManySmallFrames_IsReadAsFarAsTheStructureBudgetAllows_AndBrokenOffThere()
    {
        // 20,000 frames of one byte each: 220,000 bytes of frame headers, each read on its own.
        var tag = Tag(4, Enumerable.Range(0, 20_000).Select(_ => new Frame("TXXX", [0])));
        var reader = new BoundedReader(new MemoryByteSource(Mp3(tag)), Tight);

        var layout = Goro.Reading.Mp3.Mp3Analysis.Analyse(reader);

        var id3v2 = layout.Source(TagFormat.Id3v2)!;
        Assert.That(id3v2.State, Is.EqualTo(TagState.BrokenOff));
        Assert.That(id3v2.Fields.Length, Is.GreaterThan(0).And.LessThan(20_000));
        Assert.That(layout.Conditions.OfType<TagStructureBroken>().Single().Reason, Is.EqualTo(StructureBreak.ReadLimitReached));
        Assert.That(layout.Conditions.OfType<ReadLimitReached>().Select(c => c.Purpose), Does.Contain(ReadPurpose.DeclaredStructure));
        Assert.That(layout.IsIncomplete, Is.True);
        Assert.That(reader.Log.BytesFromSource, Is.LessThanOrEqualTo(Tight.AnalysisCeiling));
    }

    [Test]
    public void ARunOfTinyTags_IsReadAsFarAsTheSniffBudgetAllows_AndTheFileReported()
    {
        // 500 empty Id3v2 tags in a row, beyond the head window after the first hundred or so.
        var tags = Enumerable.Range(0, 500).SelectMany(_ => Tag(4, [])).ToArray();
        var reader = new BoundedReader(new MemoryByteSource(Mp3(tags)), Tight);

        var layout = Goro.Reading.Mp3.Mp3Analysis.Analyse(reader);

        Assert.That(layout.Tags.Count(t => t.Kind.Format == TagFormat.Id3v2), Is.LessThan(500));
        Assert.That(layout.Conditions.OfType<ReadLimitReached>().Select(c => c.Purpose), Does.Contain(ReadPurpose.Sniff));
        Assert.That(layout.IsIncomplete, Is.True);
        Assert.That(reader.Log.BytesFromSource, Is.LessThanOrEqualTo(Tight.AnalysisCeiling));
    }

    [Test]
    public void LongPadding_IsNotCheckedPastTheBudget_AndIsTakenAsPadding()
    {
        var tag = Tag(4, [new Frame("TIT2", [3, .. Utf8("title")])], padding: 64 * 1024);
        var reader = new BoundedReader(new MemoryByteSource(Mp3(tag)), Tight);

        var layout = Goro.Reading.Mp3.Mp3Analysis.Analyse(reader);

        Assert.That(layout.Source(TagFormat.Id3v2)!.State, Is.EqualTo(TagState.Intact));
        Assert.That(layout.IsIncomplete, Is.False, "nothing was refused: the check was not attempted");
    }

    [Test]
    public void AHealthyFile_IsNotReportedUnderTheDefaultPolicy()
    {
        foreach (var file in AudioCorpus.Mp3Files.Where(f => !f.Contains("/dmg-") && !f.Contains("/rw-")))
        {
            Assert.That(AudioCorpus.Analyse(file).Layout.Conditions.OfType<ReadLimitReached>(), Is.Empty, file);
        }
    }
}
