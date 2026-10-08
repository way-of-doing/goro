namespace Goro.Warnings;

/// <summary>
/// The four categories every warning belongs to, which <c>--no-warn</c> and the exit code refer to.
/// See docs/concepts/warnings.md.
/// </summary>
public enum WarningCategory
{
    /// <summary>Data that cannot be interpreted: an unusable occurrence was consumed.</summary>
    Data,

    /// <summary>A predicate could not be answered for some files, and the command decided for itself.</summary>
    Unanswered,

    /// <summary>A file was opened and processed, although part of what it holds could not be read.</summary>
    Incomplete,

    /// <summary>A file that cannot be read, and was therefore not processed.</summary>
    File,
}
