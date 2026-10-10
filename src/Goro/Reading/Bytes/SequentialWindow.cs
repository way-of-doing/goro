namespace Goro.Reading.Bytes;

/// <summary>
/// The bytes of the audio, read forwards in large blocks: a walk only moves forwards, so a block
/// is read once, and a read that runs past one starts the next where it is.
/// </summary>
internal sealed class SequentialWindow(IByteSource source, long end)
{
    public const int BlockLength = 1024 * 1024;

    private readonly byte[] block = new byte[BlockLength];
    private long blockStart;
    private int blockLength;

    public bool TryGet(long at, int count, out ReadOnlySpan<byte> bytes)
    {
        bytes = default;
        if (at < 0 || at + count > end || count > BlockLength)
        {
            return false;
        }

        if (at < blockStart || at + count > blockStart + blockLength)
        {
            blockStart = at;
            blockLength = (int)Math.Min(BlockLength, end - at);
            if (source.Read(at, block.AsSpan(0, blockLength)) != blockLength)
            {
                throw new IOException("The file changed while it was being read.");
            }
        }

        bytes = block.AsSpan((int)(at - blockStart), count);
        return true;
    }
}
