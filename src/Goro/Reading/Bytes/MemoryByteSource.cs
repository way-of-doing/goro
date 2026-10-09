namespace Goro.Reading.Bytes;

/// <summary>Bytes already in memory, read as though they were a file.</summary>
public sealed class MemoryByteSource(ReadOnlyMemory<byte> bytes) : IByteSource
{
    public long Length => bytes.Length;

    public int Read(long offset, Span<byte> into)
    {
        if (offset >= bytes.Length)
        {
            return 0;
        }

        var available = bytes.Span[(int)offset..];
        var count = Math.Min(available.Length, into.Length);
        available[..count].CopyTo(into);
        return count;
    }
}
