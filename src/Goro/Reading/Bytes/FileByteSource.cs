using Microsoft.Win32.SafeHandles;

namespace Goro.Reading.Bytes;

/// <summary>
/// A file on disk, opened once and read at arbitrary offsets. A symbolic link is followed to the
/// file it leads to, as <c>file::size</c> follows it.
/// </summary>
public sealed class FileByteSource : IByteSource, IDisposable
{
    private readonly SafeFileHandle handle;

    /// <exception cref="IOException">The file cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">Permission to read it is refused.</exception>
    public FileByteSource(string path)
    {
        handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
        Length = RandomAccess.GetLength(handle);
    }

    public long Length { get; }

    public int Read(long offset, Span<byte> into)
    {
        var total = 0;
        while (total < into.Length)
        {
            var read = RandomAccess.Read(handle, into[total..], offset + total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    public void Dispose() => handle.Dispose();
}
