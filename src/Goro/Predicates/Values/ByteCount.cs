namespace Goro.Predicates.Values;

/// <summary>A count of bytes. Its own type, so that it can never be mistaken for a plain number.</summary>
public readonly record struct ByteCount(decimal Bytes) : IComparable<ByteCount>
{
    public int CompareTo(ByteCount other) => Bytes.CompareTo(other.Bytes);
}
