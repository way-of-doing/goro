namespace Goro.Warnings;

/// <summary>
/// A remark about the data a run met, which stops nothing. Every warning names the file it is
/// about and belongs to exactly one <see cref="WarningCategory"/>. See docs/concepts/warnings.md.
/// </summary>
public abstract record Warning(string Path)
{
    public abstract WarningCategory Category { get; }

    /// <summary>What the warning says about <see cref="Path"/>, without the path itself.</summary>
    public abstract string Message { get; }

    /// <summary>The whole line written to standard error.</summary>
    public sealed override string ToString() => $"goro: warning: {Path}: {Message}";
}

/// <summary>
/// Data that could not be interpreted was used. Quotes the sub-expression the unusable occurrence
/// was born at, exactly as it was written in the predicate.
/// </summary>
public sealed record DataWarning(string Path, string SubExpression) : Warning(Path)
{
    public override WarningCategory Category => WarningCategory.Data;

    public override string Message => $"cannot interpret the data of {SubExpression}";
}

/// <summary>
/// A file, or a directory met while walking, could not be read. One per file, never deduplicated.
/// </summary>
public sealed record FileWarning(string Path, string Cause) : Warning(Path)
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
