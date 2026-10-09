namespace Goro.Reading.Bytes;

/// <summary>
/// Where a file's bytes come from: the file itself in production, an array in tests. A source does
/// no bookkeeping of its own; <see cref="BoundedReader"/> decides what may be read and records it.
/// </summary>
/// <remarks>
/// A source is not disposable as such: <see cref="BoundedReader"/> only reads from one, and
/// whoever creates a source that holds something, as <see cref="FileByteSource"/> holds a file
/// handle, owns it by its own type and disposes it.
/// </remarks>
public interface IByteSource
{
    long Length { get; }

    /// <summary>
    /// Reads up to <paramref name="into"/>'s length from <paramref name="offset"/>, returning how many
    /// bytes were read: fewer only at the end of the source.
    /// </summary>
    int Read(long offset, Span<byte> into);
}
