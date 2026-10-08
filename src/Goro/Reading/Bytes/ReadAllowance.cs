namespace Goro.Reading.Bytes;

/// <summary>
/// What is left of one file's <see cref="ReadPolicy"/>: the policy is shared and immutable, and each
/// file's reader takes an allowance of its own from it, which decides whether a read may be made and
/// charges it. Reads served from the head and tail windows are not charged.
/// </summary>
/// <remarks>
/// Sniffing, searching and declared structure each have a budget for the whole file, however many
/// reads make it up, so that no file can be walked from end to end by reads that are each small. A
/// payload is held to a limit for each value instead, and charged nothing: values are read when a
/// predicate asks for them, and a budget shared between them would make whether one can be read
/// depend on which others were read first. One allowance serves one file, from one thread.
/// </remarks>
public sealed class ReadAllowance
{
    private readonly long[] remaining;

    internal ReadAllowance(ReadPolicy policy)
    {
        Policy = policy;
        remaining = new long[Enum.GetValues<ReadPurpose>().Length];
        remaining[(int)ReadPurpose.Sniff] = policy.SniffBudget;
        remaining[(int)ReadPurpose.Search] = policy.SearchBudget;
        remaining[(int)ReadPurpose.DeclaredStructure] = policy.StructureBudget;
    }

    public ReadPolicy Policy { get; }

    /// <summary>Whether a read of <paramref name="count"/> bytes for <paramref name="purpose"/> would be allowed.</summary>
    public bool Allows(ReadPurpose purpose, long count) =>
        purpose == ReadPurpose.Payload ? count <= Policy.PayloadLimit : count <= remaining[(int)purpose];

    /// <summary>Charges a read to its purpose, if it is allowed; otherwise charges nothing.</summary>
    public bool TryCharge(ReadPurpose purpose, long count)
    {
        if (!Allows(purpose, count))
        {
            return false;
        }

        if (purpose != ReadPurpose.Payload)
        {
            remaining[(int)purpose] -= count;
        }

        return true;
    }

    /// <summary>What is left of a purpose's budget; a payload's limit, for a payload.</summary>
    public long Remaining(ReadPurpose purpose) =>
        purpose == ReadPurpose.Payload ? Policy.PayloadLimit : remaining[(int)purpose];
}
