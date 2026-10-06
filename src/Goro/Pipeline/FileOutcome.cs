using System.Collections.Immutable;
using Goro.Warnings;

namespace Goro.Pipeline;

/// <summary>
/// What one file counts towards in the run outcome, which is what <c>--strict-exit-code</c> reads.
/// See docs/concepts/exit-codes.md.
/// </summary>
public enum FileDisposition
{
    /// <summary>The file could not be read, was not processed, and does not count as examined.</summary>
    Unreadable,

    /// <summary>The file was examined and did not match.</summary>
    Unmatched,

    /// <summary>The file was examined and matched.</summary>
    Matched,
}

/// <summary>
/// Everything a pipeline hands back for one file: the result to render, if there is one, what the
/// file counts towards, and the warnings it produced, which reach standard error together.
///
/// Whether the file's predicate could not be answered is a separate matter from its disposition, which
/// is the command's decision about such a file: <c>goro list</c> leaves it out, while a command
/// bound to account for every file would keep it. Either way the run counts it, for the one warning
/// that reports such files (docs/concepts/warnings.md).
///
/// A file that could not be read carries its one file warning and nothing else; any data warning met
/// before the file turned out to be unreadable is not emitted (docs/concepts/warnings.md). The
/// factories are the only way to build an outcome, so that rule cannot be broken by accident.
/// </summary>
public sealed record FileOutcome<TResult>
    where TResult : class
{
    private FileOutcome(TResult? output, FileDisposition disposition, ImmutableArray<Warning> warnings, bool isUnanswered)
    {
        Output = output;
        Disposition = disposition;
        Warnings = warnings;
        IsUnanswered = isUnanswered;
    }

    /// <summary>What the renderer writes for this file, or null when the file is left out of the output.</summary>
    public TResult? Output { get; }

    public FileDisposition Disposition { get; }

    public ImmutableArray<Warning> Warnings { get; }

    /// <summary>
    /// The file's predicate evaluated to unusable, so its disposition is what the command chose for a
    /// file it could not decide about rather than an answer.
    /// </summary>
    public bool IsUnanswered { get; }

    /// <summary>The file was examined, matched, and appears in the output.</summary>
    public static FileOutcome<TResult> Matched(TResult output, IEnumerable<Warning>? warnings = null, bool unanswered = false) =>
        new(output, FileDisposition.Matched, [.. warnings ?? []], unanswered);

    /// <summary>The file was examined, did not match, and is left out of the output.</summary>
    public static FileOutcome<TResult> Unmatched(IEnumerable<Warning>? warnings = null, bool unanswered = false) =>
        new(null, FileDisposition.Unmatched, [.. warnings ?? []], unanswered);

    /// <summary>
    /// The file could not be read. Whether it still appears in the output is the command's to say:
    /// <c>goro hash</c> shows it with its hash absent, <c>goro list</c> leaves it out.
    /// </summary>
    public static FileOutcome<TResult> Unreadable(FileWarning warning, TResult? output = null) =>
        new(output, FileDisposition.Unreadable, [warning], isUnanswered: false);
}
