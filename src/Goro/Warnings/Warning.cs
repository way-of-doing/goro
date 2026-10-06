namespace Goro.Warnings;

/// <summary>
/// A remark about the data a run met, which stops nothing. Every warning names where to look -- a
/// file, a directory, or the run as a whole -- and belongs to exactly one
/// <see cref="WarningCategory"/>. See docs/concepts/warnings.md.
/// </summary>
public abstract record Warning
{
    public abstract WarningCategory Category { get; }

    /// <summary>The whole line written to standard error.</summary>
    public abstract override string ToString();
}

/// <summary>A warning about one file, or one directory, which it names before saying anything else.</summary>
public abstract record PathWarning(string Path) : Warning
{
    /// <summary>What the warning says about <see cref="Path"/>, without the path itself.</summary>
    public abstract string Message { get; }

    public sealed override string ToString() => $"goro: warning: {Path}: {Message}";
}

/// <summary>
/// Data that could not be interpreted was used. Quotes the sub-expression the unusable occurrence
/// was born at, exactly as it was written in the predicate.
/// </summary>
public sealed record DataWarning(string Path, string SubExpression) : PathWarning(Path)
{
    public override WarningCategory Category => WarningCategory.Data;

    public override string Message => $"cannot interpret the data of {SubExpression}";
}

/// <summary>
/// A file, or a directory met while walking, could not be read. One per file, never deduplicated.
/// </summary>
public sealed record FileWarning(string Path, string Cause) : PathWarning(Path)
{
    public override WarningCategory Category => WarningCategory.File;

    public override string Message => $"cannot be read: {Cause}";

    /// <summary>
    /// A file warning for <paramref name="path"/> whose cause is described by what was thrown while
    /// reading it. The common causes get a short description of their own, since the messages the
    /// runtime gives them repeat the path the warning already names.
    /// </summary>
    public static FileWarning From(string path, Exception exception) => new(path, Describe(exception));

    private static string Describe(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => "no such file or directory",
        UnauthorizedAccessException => "permission denied",
        _ => exception.Message,
    };
}

/// <summary>
/// The predicate could not be answered for some of the files a run examined, and the command decided
/// for itself what to do with them. One per run, written once every file has been processed. What it
/// says is the command's own, since what it reports is the command's decision.
/// </summary>
public sealed record UnansweredWarning(string Message) : Warning
{
    public override WarningCategory Category => WarningCategory.Unanswered;

    public override string ToString() => $"goro: warning: {Message}";
}
