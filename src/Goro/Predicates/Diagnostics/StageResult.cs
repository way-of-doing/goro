using System.Collections.Immutable;

namespace Goro.Predicates.Diagnostics;

/// <summary>
/// What one stage of reading a predicate produced: either a value, or the errors that prevented it.
/// </summary>
public sealed class StageResult<T>
{
    private readonly T? value;

    private StageResult(T? value, ImmutableArray<Diagnostic> diagnostics)
    {
        this.value = value;
        Diagnostics = diagnostics;
    }

    public static StageResult<T> Success(T value) => new(value, []);

    public static StageResult<T> Failure(IEnumerable<Diagnostic> diagnostics)
    {
        var all = diagnostics.ToImmutableArray();
        if (all.IsEmpty)
        {
            throw new ArgumentException("A failure needs at least one diagnostic.", nameof(diagnostics));
        }

        return new(default, all);
    }

    public static StageResult<T> Failure(Diagnostic diagnostic) => Failure([diagnostic]);

    public bool Succeeded => Diagnostics.IsEmpty;

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public T Value => Succeeded
        ? value!
        : throw new InvalidOperationException("A stage that failed has no value.");

    /// <summary>Runs the next stage on this one's value, or passes this one's errors along.</summary>
    public StageResult<TNext> Then<TNext>(Func<T, StageResult<TNext>> next) =>
        Succeeded ? next(Value) : StageResult<TNext>.Failure(Diagnostics);
}
