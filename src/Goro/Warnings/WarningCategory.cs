namespace Goro.Warnings;

/// <summary>
/// The two categories every warning belongs to, which <c>--no-warn</c> and the exit code refer to.
/// See docs/concepts/warnings.md.
/// </summary>
public enum WarningCategory
{
    /// <summary>Data that cannot be interpreted: an unusable occurrence was consumed.</summary>
    Data,

    /// <summary>A file that cannot be read, and was therefore not processed.</summary>
    File,
}
