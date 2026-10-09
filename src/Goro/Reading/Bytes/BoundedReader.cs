namespace Goro.Reading.Bytes;

/// <summary>
/// The only way the analysis reaches a file's bytes. The head and the tail are read once each, into
/// windows that serve every read falling inside them; any other read must be allowed by the file's
/// <see cref="ReadAllowance"/>, taken from the shared <see cref="ReadPolicy"/>, and is refused when it
/// is not. Every read is logged.
/// </summary>
/// <remarks>
/// One reader serves one file, from one thread, so it takes no locks. A refusal is an answer, not an
/// exception: what a file declares is never trusted to be readable. Only the source failing, or the
/// file shrinking while it is read, throws.
/// </remarks>
public sealed class BoundedReader
{
    private readonly IByteSource source;
    private readonly long headLength;
    private readonly long tailStart;
    private readonly ReadAllowance allowance;
    private byte[]? head;
    private byte[]? tail;

    public BoundedReader(IByteSource source, ReadPolicy policy)
    {
        this.source = source;
        Policy = policy;
        allowance = policy.CreateAllowance();
        Length = source.Length;

        // A file that both windows would cover is read whole, as the head.
        if (Length <= (long)policy.HeadWindow + policy.TailWindow)
        {
            headLength = Length;
            tailStart = Length;
        }
        else
        {
            headLength = policy.HeadWindow;
            tailStart = Length - policy.TailWindow;
        }
    }

    public long Length { get; }

    public ReadPolicy Policy { get; }

    public ReadLog Log { get; } = new();

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes at <paramref name="offset"/>, or refuses: when the
    /// range is not wholly inside the file, or does not fall inside a window and
    /// <paramref name="purpose"/> cannot afford it.
    /// </summary>
    /// <exception cref="IOException">The source failed, or the file changed while it was read.</exception>
    public bool TryRead(ReadPurpose purpose, long offset, long count, out ReadOnlyMemory<byte> bytes)
    {
        bytes = default;
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            Log.Add(new(purpose, offset, count, ReadOutcome.OutsideFile));
            return false;
        }

        if (InWindow(offset, count))
        {
            bytes = offset + count <= headLength
                ? Head().AsMemory((int)offset, (int)count)
                : Tail().AsMemory((int)(offset - tailStart), (int)count);
            Log.Add(new(purpose, offset, count, ReadOutcome.FromWindow));
            return true;
        }

        if (!allowance.TryCharge(purpose, count))
        {
            Log.Add(new(purpose, offset, count, ReadOutcome.OverLimit));
            return false;
        }

        bytes = ReadFromSource(purpose, offset, (int)count);
        return true;
    }

    /// <summary>Whether a read would be served, without making it: what lets a caller decline one it need not make.</summary>
    public bool CanRead(ReadPurpose purpose, long offset, long count) =>
        offset >= 0 && count >= 0 && offset <= Length - count && (InWindow(offset, count) || allowance.Allows(purpose, count));

    private bool InWindow(long offset, long count) => offset + count <= headLength || offset >= tailStart;

    private byte[] Head() => head ??= ReadFromSource(ReadPurpose.Sniff, 0, (int)headLength);

    private byte[] Tail() => tail ??= ReadFromSource(ReadPurpose.Sniff, tailStart, (int)(Length - tailStart));

    private byte[] ReadFromSource(ReadPurpose purpose, long offset, int count)
    {
        var buffer = new byte[count];
        if (source.Read(offset, buffer) != count)
        {
            throw new IOException("The file changed while it was being read.");
        }

        Log.Add(new(purpose, offset, count, ReadOutcome.FromSource));
        return buffer;
    }
}
