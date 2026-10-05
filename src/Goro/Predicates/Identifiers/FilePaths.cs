namespace Goro.Predicates.Identifiers;

/// <summary>
/// How a path is written when a predicate sees it as <c>file::path</c>: exactly as discovered, but
/// with <c>/</c> as the separator on every platform, so that one predicate means the same thing
/// everywhere. See the <c>file</c> namespace in docs/features/builtins/identifiers.md.
/// </summary>
public static class FilePaths
{
    /// <summary>The path written with <c>/</c> in place of this platform's directory separator.</summary>
    public static string ToPredicatePath(string path) => ToPredicatePath(path, Path.DirectorySeparatorChar);

    /// <summary>
    /// The path written with <c>/</c> in place of <paramref name="directorySeparator"/>. Only the
    /// separator is replaced: on a platform whose separator is <c>/</c>, a backslash is an ordinary
    /// character of a file name and is left alone. Taking the separator as an argument is what lets
    /// the Windows rule be tested on any platform.
    /// </summary>
    public static string ToPredicatePath(string path, char directorySeparator) =>
        directorySeparator == '/' ? path : path.Replace(directorySeparator, '/');
}
