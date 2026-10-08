namespace Goro.Reading.Bytes;

/// <summary>How a read was served.</summary>
public enum ReadOutcome
{
    /// <summary>From the head or tail window, read once already.</summary>
    FromWindow,

    /// <summary>From the source: a window being filled, or a read outside both windows.</summary>
    FromSource,

    /// <summary>Not at all: the range is not wholly inside the file. This is ordinary probing, such as looking for a footer in a short file.</summary>
    OutsideFile,

    /// <summary>Not at all: the purpose's budget, or a payload's limit, does not allow it.</summary>
    OverLimit,
}

public readonly record struct ReadRecord(ReadPurpose Purpose, long Offset, long Length, ReadOutcome Outcome);

/// <summary>
/// Every read made of one file, in order, with what it was for and how it was served: what lets a
/// test assert that a file was read only at its edges.
/// </summary>
public sealed class ReadLog
{
    private readonly List<ReadRecord> records = [];

    public IReadOnlyList<ReadRecord> Records => records;

    /// <summary>How many bytes were taken from the source, windows included.</summary>
    public long BytesFromSource { get; private set; }

    /// <summary>Whether a read for <paramref name="purpose"/> was refused for want of budget or over a limit.</summary>
    public bool WentOverLimit(ReadPurpose purpose) =>
        records.Any(record => record.Purpose == purpose && record.Outcome == ReadOutcome.OverLimit);

    internal void Add(ReadRecord record)
    {
        records.Add(record);
        if (record.Outcome == ReadOutcome.FromSource)
        {
            BytesFromSource += record.Length;
        }
    }
}
