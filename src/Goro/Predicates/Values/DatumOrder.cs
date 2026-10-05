namespace Goro.Predicates.Values;

/// <summary>
/// How an operator compares the data of one type. <see cref="Prepare"/> is applied to each datum
/// once before it is compared, which for strings is where normalization happens; comparing two
/// prepared data is then a plain ordering.
/// </summary>
public abstract class DatumOrder<T> where T : notnull
{
    public virtual T Prepare(T datum) => datum;

    public abstract int Compare(T x, T y);
}

/// <summary>The order of a type that has a natural one: numbers, bytecounts, durations, booleans.</summary>
public sealed class NaturalOrder<T> : DatumOrder<T> where T : notnull, IComparable<T>
{
    public static NaturalOrder<T> Instance { get; } = new();

    private NaturalOrder()
    {
    }

    public override int Compare(T x, T y) => x.CompareTo(y);
}
