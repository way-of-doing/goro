// Owned by the identifier catalog group (G7) of the predicate-runtime-architecture line.
using System.Runtime.ExceptionServices;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// One file's data, loaded only as far as an evaluation asks for it. Each facet is loaded at most
/// once, on first use; if loading fails, the file is unreadable, and every later request for that
/// facet fails the same way.
/// </summary>
/// <remarks>
/// A <see cref="FileData"/> belongs to one evaluation of one file, which runs on one thread from
/// start to finish, so it takes no locks. It must not be shared between concurrent evaluations;
/// what they share is the facets, which keep no state of their own.
/// </remarks>
public sealed class FileData
{
    // Keyed by facet instance, since that is what identifies a facet. Each entry is either the
    // facet's data or the Failure it ended in; a file meets only a handful of facets, so this
    // stays tiny.
    private readonly Dictionary<object, object> loaded = new(ReferenceEqualityComparer.Instance);

    /// <param name="path">The file's full path, as discovered.</param>
    public FileData(string path) => Path = path;

    public string Path { get; }

    /// <summary>Whether a facet that opens the file has been loaded, or tried.</summary>
    public bool Opened { get; private set; }

    /// <exception cref="UnreadableFileException">The facet cannot be loaded.</exception>
    public T Get<T>(FileFacet<T> facet) where T : notnull
    {
        if (!loaded.TryGetValue(facet, out var entry))
        {
            Opened |= facet.OpensFile;
            entry = Load(facet);
            loaded.Add(facet, entry);
        }

        if (entry is Failure failure)
        {
            failure.Rethrow();
        }

        return (T)entry;
    }

    private object Load<T>(FileFacet<T> facet) where T : notnull
    {
        try
        {
            return facet.Load(Path);
        }
        catch (UnreadableFileException exception)
        {
            return new Failure(exception);
        }
        catch (Exception exception)
        {
            return new Failure(new UnreadableFileException(Path, exception.Message, exception));
        }
    }

    /// <summary>
    /// A facet that could not be loaded. The same exception is thrown for every request, so that
    /// however many identifiers asked, the file was found unreadable once and for one reason.
    /// </summary>
    private sealed class Failure(UnreadableFileException exception)
    {
        private readonly ExceptionDispatchInfo dispatch = ExceptionDispatchInfo.Capture(exception);

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public void Rethrow() => dispatch.Throw();
    }
}
