namespace Goro.Predicates.Values;

/// <summary>
/// A length of time in whole seconds. A duration read from a file is truncated to whole seconds
/// when it is produced, so that equality between durations means what it appears to.
/// </summary>
public readonly record struct Duration(decimal Seconds) : IComparable<Duration>
{
    public int CompareTo(Duration other) => Seconds.CompareTo(other.Seconds);
}
