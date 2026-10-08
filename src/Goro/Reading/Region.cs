namespace Goro.Reading;

/// <summary>A run of bytes in a file: where it starts, and how long it is.</summary>
public readonly record struct Region(long Start, long Length)
{
    public long End => Start + Length;

    public static Region Between(long start, long end) => new(start, end - start);

    public override string ToString() => $"[{Start}, {End})";
}
