using System.Collections.Frozen;

namespace Goro.Warnings;

/// <summary>
/// Writes warnings to a text writer, normally standard error, one whole line each.
///
/// Suppression is decided here and nowhere else: a warning of a suppressed category is dropped on
/// arrival, so it reaches neither the writer nor <see cref="Produced"/>, and nothing records that it
/// ever existed. "A suppressed warning was not produced" (docs/concepts/warnings.md) then holds by
/// construction rather than by every producer remembering to check.
///
/// Batches arrive from many concurrent pipeline invocations. A lock keeps each batch contiguous on
/// the writer; it is held only for the few lines of one file, and nothing else in the run waits on
/// it.
/// </summary>
public sealed class WarningSink(TextWriter writer, IReadOnlySet<WarningCategory> suppressed) : IWarningSink
{
    private readonly Lock _gate = new();
    private readonly HashSet<WarningCategory> _produced = [];

    public void Emit(IReadOnlyList<Warning> batch)
    {
        var kept = batch.Where(w => !suppressed.Contains(w.Category)).ToList();
        if (kept.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            foreach (var warning in kept)
            {
                writer.WriteLine(warning.ToString());
                _produced.Add(warning.Category);
            }
        }
    }

    public IReadOnlySet<WarningCategory> Produced
    {
        get
        {
            lock (_gate)
            {
                return _produced.ToFrozenSet();
            }
        }
    }
}
