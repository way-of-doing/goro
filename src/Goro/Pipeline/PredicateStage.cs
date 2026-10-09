using Goro.Domain;
using Goro.Predicates;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Reading.Bytes;
using Goro.Warnings;

namespace Goro.Pipeline;

/// <summary>
/// Lists the files that satisfy a predicate: <c>goro list --filter</c>. See docs/commands/list.md.
///
/// The predicate keeps true, false and unusable apart, and this is where <c>list</c> decides what
/// each means: a true file is listed, a false or unusable one is not, and all three were examined.
/// An unusable one is also counted as unanswered, and <see cref="Unanswered"/> is how <c>list</c>
/// reports those at the end of the run.
/// A file found unreadable while the predicate was being evaluated is not listed, is not counted as
/// examined, and reports its one file warning and nothing else, any data warning met on the way
/// being dropped (decision D3 of docs/design/predicate-runtime.md).
/// </summary>
/// <remarks>
/// The compiled predicate is shared by every concurrent invocation; what belongs to one file, its
/// data as it is loaded and the sources it has reported, lives in an evaluation context made for
/// that file and dropped with it. Evaluation is synchronous (decision D2) and runs on whichever
/// thread-pool thread the executor gave this invocation.
///
/// Only <see cref="UnreadableFileException"/> is caught. Anything else is a defect rather than a
/// bad file, and fails the run.
/// </remarks>
public sealed class PredicateStage(CompiledPredicate predicate, ReadPolicy? policy = null) : IPipelineStage<string, FileOutcome<ListResult>>
{
    private readonly ReadPolicy policy = policy ?? ReadPolicy.Default;

    public CompiledPredicate Predicate { get; } = predicate;

    public Task<FileOutcome<ListResult>> ExecuteAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Decide(filePath));
    }

    private FileOutcome<ListResult> Decide(string filePath)
    {
        // One loader serves every facet of the file, and closes it when the file is done.
        using var loader = new FileDataLoader(filePath, policy);
        var context = new EvaluationContext(new FileData(filePath, loader), Predicate.Sources.Count);

        Truth truth;
        try
        {
            truth = Predicate.Evaluate(context);
        }
        catch (UnreadableFileException ex)
        {
            return FileOutcome<ListResult>.Unreadable(FileWarningFor(filePath, ex));
        }

        var warnings = context.Reported.Select(origin => new DataWarning(filePath, origin.Text));
        var reading = !loader.Opened ? FileReading.NotOpened : loader.Layout.IsIncomplete ? FileReading.ReadInPart : FileReading.Read;
        return truth == Truth.True
            ? FileOutcome<ListResult>.Matched(new ListResult(filePath), warnings, reading: reading)
            : FileOutcome<ListResult>.Unmatched(warnings, unanswered: truth == Truth.Unusable, reading: reading);
    }

    /// <summary>
    /// The warning <c>goro list</c> ends a run with when its predicate could not be answered for some
    /// of the files it examined, saying how many and that they were not listed.
    /// </summary>
    /// <param name="unanswered">How many files the predicate could not be answered for; at least one.</param>
    /// <param name="examined">How many files were examined, those included.</param>
    public static UnansweredWarning Unanswered(int unanswered, int examined) => new((unanswered, examined) switch
    {
        (1, 1) => "the predicate could not be answered for the one file examined, which was not listed",
        (1, _) => $"the predicate could not be answered for 1 of the {examined} files examined, which was not listed",
        _ when unanswered == examined => $"the predicate could not be answered for any of the {examined} files examined, which were not listed",
        _ => $"the predicate could not be answered for {unanswered} of the {examined} files examined, which were not listed",
    });

    // The cause is described from what the file system or the tag library threw, where there is
    // such a thing, so that a file gone or refused reads the same here as under goro hash.
    private static FileWarning FileWarningFor(string filePath, UnreadableFileException exception) =>
        exception.InnerException is { } inner
            ? FileWarning.From(filePath, inner)
            : new FileWarning(filePath, exception.Reason);
}
