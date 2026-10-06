using Goro.Cli;
using Goro.Execution;
using Goro.Warnings;

namespace Goro.Tests.Cli;

/// <summary>
/// The precedence rules of docs/concepts/exit-codes.md, at the level of the run outcome, where every
/// combination can be stated directly: data warnings, which need a predicate to provoke end to end,
/// included.
/// </summary>
public class ExitCodesTests
{
    private const WarningCategory Data = WarningCategory.Data;
    private const WarningCategory Unanswered = WarningCategory.Unanswered;
    private const WarningCategory File = WarningCategory.File;

    private static RunOutcome Outcome(int found, int examined, int matched, params WarningCategory[] warned) =>
        new(found, examined, matched, warned.ToHashSet());

    private static IEnumerable<TestCaseData> StrictCases()
    {
        // Nothing to report.
        yield return Case("every file matched", Outcome(3, 3, 3), ExitCodes.Completed);
        yield return Case("some files matched", Outcome(3, 3, 1), ExitCodes.Completed);

        // One condition at a time.
        yield return Case("a data warning", Outcome(3, 3, 3, Data), ExitCodes.DataWarnings);
        yield return Case("a file warning", Outcome(3, 2, 2, File), ExitCodes.FileWarnings);
        yield return Case("an unanswered predicate", Outcome(3, 3, 2, Unanswered), ExitCodes.UnansweredPredicates);
        yield return Case("examined, none matched", Outcome(3, 3, 0), ExitCodes.NothingMatched);
        yield return Case("nothing found", Outcome(0, 0, 0), ExitCodes.NothingFound);

        // Within the 1x group, the higher code wins.
        yield return Case("unreadable file and uninterpretable tag", Outcome(3, 2, 2, Data, File), ExitCodes.FileWarnings);
        yield return Case("unanswered predicate and the data behind it", Outcome(3, 3, 2, Data, Unanswered), ExitCodes.UnansweredPredicates);
        yield return Case("unanswered predicate and an unreadable file", Outcome(3, 2, 1, Unanswered, File), ExitCodes.FileWarnings);
        yield return Case("all three", Outcome(3, 2, 1, Data, Unanswered, File), ExitCodes.FileWarnings);

        // The 1x group beats the 2x group.
        yield return Case("data warning and an empty result", Outcome(3, 3, 0, Data), ExitCodes.DataWarnings);
        yield return Case("unanswered predicate and an empty result", Outcome(1, 1, 0, Unanswered), ExitCodes.UnansweredPredicates);
        yield return Case("file warning and an empty result", Outcome(3, 2, 0, File), ExitCodes.FileWarnings);
        yield return Case("both warnings and an empty result", Outcome(3, 2, 0, Data, File), ExitCodes.FileWarnings);
        yield return Case("an unlistable directory and nothing found", Outcome(0, 0, 0, File), ExitCodes.FileWarnings);

        // An unreadable file is found but not examined.
        yield return Case("the only file was unreadable", Outcome(1, 0, 0, File), ExitCodes.FileWarnings);
        yield return Case("unreadable file in a run where nothing matched", Outcome(2, 1, 0, File), ExitCodes.FileWarnings);
        yield return Case("only unreadable files, their warnings suppressed", Outcome(2, 0, 0), ExitCodes.Completed);

        // Suppression: a suppressed category simply never appears in the outcome, which is what
        // makes the 2x codes reachable.
        yield return Case("data warnings suppressed, nothing matched", Outcome(3, 3, 0), ExitCodes.NothingMatched);
        yield return Case("file warnings suppressed, data warning fired", Outcome(3, 2, 1, Data), ExitCodes.DataWarnings);
        yield return Case("data warnings suppressed, a predicate unanswered", Outcome(1, 1, 0, Unanswered), ExitCodes.UnansweredPredicates);
        yield return Case("unanswered suppressed, data warning fired", Outcome(1, 1, 0, Data), ExitCodes.DataWarnings);
    }

    private static TestCaseData Case(string name, RunOutcome outcome, int expected) =>
        new TestCaseData(outcome, expected).SetName($"strict: {name} -> {expected}");

    [TestCaseSource(nameof(StrictCases))]
    public void For_Strict_ChoosesByPrecedence(RunOutcome outcome, int expected)
    {
        Assert.That(ExitCodes.For(outcome, strict: true), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(StrictCases))]
    public void For_NotStrict_IsAlwaysZero(RunOutcome outcome, int _)
    {
        Assert.That(ExitCodes.For(outcome, strict: false), Is.EqualTo(ExitCodes.Completed));
    }
}
