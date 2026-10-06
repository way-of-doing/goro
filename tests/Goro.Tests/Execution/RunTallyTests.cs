using Goro.Execution;
using Goro.Pipeline;
using Goro.Tests.TestSupport;
using Goro.Warnings;

namespace Goro.Tests.Execution;

public class RunTallyTests
{
    [Test]
    public void Outcome_NothingRecorded_FoundNothing()
    {
        var tally = new RunTally(new RecordingWarningSink());

        Assert.That(tally.Outcome, Has.Property(nameof(RunOutcome.Found)).EqualTo(0)
            .And.Property(nameof(RunOutcome.Examined)).EqualTo(0)
            .And.Property(nameof(RunOutcome.Matched)).EqualTo(0));
        Assert.That(tally.Outcome.Warned, Is.Empty);
    }

    [Test]
    public void Record_CountsEachDisposition_AnUnreadableFileBeingFoundButNotExamined()
    {
        var tally = new RunTally(new RecordingWarningSink());

        tally.Record(FileOutcome<string>.Matched("a"));
        tally.Record(FileOutcome<string>.Matched("b"));
        tally.Record(FileOutcome<string>.Unmatched());
        tally.Record(FileOutcome<string>.Unreadable(new FileWarning("d", "damaged"), "d"));

        Assert.That(tally.Outcome, Has.Property(nameof(RunOutcome.Found)).EqualTo(4)
            .And.Property(nameof(RunOutcome.Examined)).EqualTo(3)
            .And.Property(nameof(RunOutcome.Matched)).EqualTo(2));
    }

    [Test]
    public void Record_CountsTheUnansweredFiles_WhateverTheirDisposition()
    {
        var tally = new RunTally(new RecordingWarningSink());

        tally.Record(FileOutcome<string>.Unmatched(unanswered: true));
        tally.Record(FileOutcome<string>.Matched("b", unanswered: true));
        tally.Record(FileOutcome<string>.Unmatched());
        tally.Record(FileOutcome<string>.Matched("d"));
        tally.Record(FileOutcome<string>.Unreadable(new FileWarning("e", "damaged")));

        Assert.That(tally.Outcome, Has.Property(nameof(RunOutcome.Unanswered)).EqualTo(2)
            .And.Property(nameof(RunOutcome.Examined)).EqualTo(4));
    }

    [Test]
    public void Record_HandsTheFilesWarningsToTheSinkTogether_AndOutcomeReportsTheirCategories()
    {
        var sink = new RecordingWarningSink();
        var tally = new RunTally(sink);
        DataWarning[] warnings = [new("a", "x"), new("a", "y")];

        tally.Record(FileOutcome<string>.Unmatched(warnings));
        tally.Record(FileOutcome<string>.Matched("b"));

        Assert.That(sink.Batches, Has.Count.EqualTo(1));
        Assert.That(sink.Batches[0], Is.EqualTo(warnings));
        Assert.That(tally.Outcome.Warned, Is.EquivalentTo(new[] { WarningCategory.Data }));
    }

    [Test]
    public void Record_FromManyThreads_LosesNothing()
    {
        var tally = new RunTally(new RecordingWarningSink());

        Parallel.For(0, 3000, i => tally.Record((i % 3) switch
        {
            0 => FileOutcome<string>.Matched("m"),
            1 => FileOutcome<string>.Unmatched(),
            _ => FileOutcome<string>.Unreadable(new FileWarning($"f{i}", "gone")),
        }));

        Assert.That(tally.Outcome, Has.Property(nameof(RunOutcome.Found)).EqualTo(3000)
            .And.Property(nameof(RunOutcome.Examined)).EqualTo(2000)
            .And.Property(nameof(RunOutcome.Matched)).EqualTo(1000));
    }

    // D3: a file that turns out to be unreadable reports its file warning and nothing else.
    [Test]
    public void Unreadable_CarriesOnlyItsFileWarning()
    {
        var warning = new FileWarning("a", "damaged");

        var outcome = FileOutcome<string>.Unreadable(warning);

        Assert.That(outcome.Warnings, Is.EqualTo(new[] { warning }));
        Assert.That(outcome.Output, Is.Null);
    }
}
