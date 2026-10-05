// Owned by the evaluator group (G5) of the predicate-runtime-architecture line.
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// Everything that belongs to evaluating one predicate against one file, and nothing that outlives
/// it: the file's data as it is loaded, and the sources reported so far. The compiled predicate is
/// shared by every concurrent evaluation; this is not.
/// </summary>
/// <remarks>
/// Sources are small integers interned when the predicate was read, so the set of sources already
/// reported can be indexed by them directly. The rationale describes that set as a bitset; since
/// an origin of each source has to be kept as well, for the warning to quote, it is an array of
/// origins indexed by source instead, an empty slot being a source not yet reported. Most files
/// report nothing, so the array is not allocated until something is.
/// </remarks>
public sealed class EvaluationContext
{
    private readonly int sourceCount;
    private Origin?[]? origins;

    /// <param name="sourceCount">The number of distinct sources the predicate can report.</param>
    public EvaluationContext(FileData file, int sourceCount)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceCount);
        File = file;
        this.sourceCount = sourceCount;
    }

    public FileData File { get; }

    /// <summary>
    /// Records that an unusable occurrence born at <paramref name="origin"/> was consumed. Of the
    /// origins reported for one source, the one written earliest in the predicate is kept: the
    /// order of the reports depends on the order of the occurrences in a bag, which has none.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The source is not one the predicate can report.</exception>
    public void Report(Origin origin)
    {
        var source = origin.Source.Value;
        if ((uint)source >= (uint)sourceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(origin), source, $"The predicate can report {sourceCount} sources, numbered from 0.");
        }

        origins ??= new Origin?[sourceCount];
        if (origins[source] is not { } kept || origin.Start < kept.Start)
        {
            origins[source] = origin;
        }
    }

    /// <summary>
    /// The origins reported for this file, one per source, in ascending order of source. The origin
    /// kept for a source is the one written earliest, whose text is what a warning quotes. Ordering by
    /// source rather than by time keeps a file's warnings independent of the order of the
    /// occurrences in a bag, which the specification declares to have none.
    /// </summary>
    public IReadOnlyList<Origin> Reported =>
        origins is null ? [] : [.. origins.OfType<Origin>()];
}
