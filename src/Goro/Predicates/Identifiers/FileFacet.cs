namespace Goro.Predicates.Identifiers;

/// <summary>
/// One kind of data about a file that is loaded as a whole -- its file system metadata, its audio
/// properties, its tags. A facet is identified by its instance, is shared by every evaluation, and
/// keeps no state.
/// </summary>
public abstract class FileFacet<T> where T : notnull
{
    /// <summary>Loads this facet's data for the file at <paramref name="path"/>.</summary>
    /// <exception cref="Exception">Any failure; <see cref="FileData"/> turns it into an unreadable file.</exception>
    public abstract T Load(string path);
}
