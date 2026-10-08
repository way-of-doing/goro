using Goro.Reading;
using Goro.Reading.Tags;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// How one file's contents are reached: the analysis of its edges, and its tag values on demand.
/// <see cref="FileData"/> holds one and does not know how it works; the runtime creates it for each
/// file, and disposes it when the file is done.
/// </summary>
public interface IFileDataLoader
{
    /// <summary>Whether the file's contents have been read: what the <c>incomplete</c> warning counts by.</summary>
    bool Opened { get; }

    /// <summary>The analysis of the file's edges, made the first time it is asked for.</summary>
    /// <exception cref="IOException">The file cannot be read: it has gone, or the disk failed.</exception>
    /// <exception cref="UnauthorizedAccessException">Permission to read it is refused.</exception>
    FileLayout Layout { get; }

    /// <summary>The file's tag values, read when asked for, through the same reader as the layout.</summary>
    TagValues Values { get; }
}
