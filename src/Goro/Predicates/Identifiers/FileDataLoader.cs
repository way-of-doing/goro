using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Reading.Tags;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// Reads one file from disk: the file is opened the first time anything inside it is needed, read
/// through one <see cref="BoundedReader"/> for everything asked of it, and closed when the loader
/// is disposed. A file nothing needed opening is never opened.
/// </summary>
/// <remarks>One loader serves one file, from one thread.</remarks>
public sealed class FileDataLoader(string path, ReadPolicy policy) : IFileDataLoader, IDisposable
{
    private FileByteSource? source;
    private BoundedReader? reader;
    private FileLayout? layout;
    private TagValues? values;

    public bool Opened => source is not null;

    public FileLayout Layout => layout ??= Mp3Analysis.Analyse(Reader());

    public TagValues Values => values ??= new TagValues(Reader());

    public void Dispose() => source?.Dispose();

    private BoundedReader Reader()
    {
        if (reader is null)
        {
            source = new FileByteSource(path);
            reader = new BoundedReader(source, policy);
        }

        return reader;
    }
}
