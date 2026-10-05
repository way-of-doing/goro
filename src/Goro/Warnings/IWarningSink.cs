namespace Goro.Warnings;

/// <summary>
/// Where a run's warnings go. One sink serves the whole run and is shared by every concurrent
/// pipeline invocation, so implementations are thread-safe.
/// </summary>
public interface IWarningSink
{
    /// <summary>
    /// Emits the warnings one file produced, together, so that they are never interleaved with
    /// another file's. Warnings of a suppressed category are dropped before anything else happens to
    /// them: they are written nowhere and counted nowhere.
    /// </summary>
    void Emit(IReadOnlyList<Warning> batch);

    /// <summary>The categories of which at least one warning was produced, after suppression.</summary>
    IReadOnlySet<WarningCategory> Produced { get; }
}
