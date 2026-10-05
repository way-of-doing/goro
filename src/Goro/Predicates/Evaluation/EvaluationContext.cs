// Owned by the evaluator group (G5) of the predicate-runtime-architecture line.
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// Everything that belongs to evaluating one predicate against one file, and nothing that outlives
/// it: the file's data as it is loaded, and the sources reported so far. The compiled predicate is
/// shared by every concurrent evaluation; this is not.
/// </summary>
public sealed class EvaluationContext
{
    /// <param name="sourceCount">The number of distinct sources the predicate can report.</param>
    public EvaluationContext(FileData file, int sourceCount) => throw new NotImplementedException();

    public FileData File => throw new NotImplementedException();

    /// <summary>
    /// Records that an unusable occurrence born at <paramref name="origin"/> was consumed. Only the
    /// first report of each source is kept.
    /// </summary>
    public void Report(Origin origin) => throw new NotImplementedException();

    /// <summary>The origins reported for this file, one per source, in the order first reported.</summary>
    public IReadOnlyList<Origin> Reported => throw new NotImplementedException();
}
