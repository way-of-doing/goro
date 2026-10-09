namespace Goro.Reading.Bytes;

/// <summary>
/// Why a read is made. The analysis of a file reads for the first three purposes, each with a budget
/// of its own in <see cref="ReadPolicy"/>; values are read as payloads, each with a limit of its own.
/// </summary>
public enum ReadPurpose
{
    /// <summary>A fixed-size header or footer: a tag's, a frame's.</summary>
    Sniff,

    /// <summary>The structure a tag declares: frame and item headers, or a whole tag that must be decoded before its frames can be found.</summary>
    DeclaredStructure,

    /// <summary>A bounded search for something whose position nothing declares, such as the first audio frame.</summary>
    Search,

    /// <summary>A value, read when something asks for it: a frame's content, an item's value.</summary>
    Payload,
}

/// <summary>
/// How much of a file may be read (docs/implementation.md, "Reading a file from its edges"): the
/// head and tail windows, which are read once each and serve every read that falls inside them,
/// a budget for each purpose of the analysis, and a limit on each value. The policy is shared by
/// every file and never changes; each file's reader takes a <see cref="ReadAllowance"/> from it,
/// which keeps what is left.
/// </summary>
/// <remarks>
/// The analysis can therefore take from any file at most the two windows and the three budgets,
/// <see cref="AnalysisCeiling"/>.
/// </remarks>
/// <param name="StructureBudget">
/// What declared structure may cost in all. Frame and item headers cost little; in practice the
/// budget is there for an Id3v2 tag unsynchronised as a whole, which has to be read whole and undone
/// before its frames can be found.
/// </param>
/// <param name="PayloadLimit">The most one value may be, and also the cap on decompressing one.</param>
public sealed record ReadPolicy(
    int HeadWindow = 64 * 1024,
    int TailWindow = 64 * 1024,
    int SniffBudget = 4 * 1024,
    int SearchBudget = 256 * 1024,
    int StructureBudget = 16 * 1024 * 1024,
    int PayloadLimit = 4 * 1024 * 1024)
{
    public static ReadPolicy Default { get; } = new();

    /// <summary>A fresh allowance, for reading one file.</summary>
    public ReadAllowance CreateAllowance() => new(this);

    /// <summary>The most the analysis of any file can take from it: both windows, and every budget.</summary>
    public long AnalysisCeiling => (long)HeadWindow + TailWindow + SniffBudget + SearchBudget + StructureBudget;
}
