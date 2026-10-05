// Owned by the identifier catalog group (G7) of the predicate-runtime-architecture line.
namespace Goro.Predicates.Identifiers;

/// <summary>
/// One file's data, loaded only as far as an evaluation asks for it. Each facet is loaded at most
/// once, on first use; if loading fails, the file is unreadable, and every later request for that
/// facet fails the same way.
/// </summary>
public sealed class FileData
{
    /// <param name="path">The file's full path, as discovered.</param>
    public FileData(string path) => Path = path;

    public string Path { get; }

    /// <exception cref="UnreadableFileException">The facet cannot be loaded.</exception>
    public T Get<T>(FileFacet<T> facet) where T : notnull => throw new NotImplementedException();
}
