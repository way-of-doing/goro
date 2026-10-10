using Goro.Discovery;
using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Tags;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// Reads one file from disk: the file is opened the first time anything inside it is needed, read
/// through one <see cref="BoundedReader"/> for everything asked of it, and closed when the loader
/// is disposed. A file nothing needed opening is never opened. The file is analysed as the format
/// its extension names, as discovery judged it.
/// </summary>
/// <remarks>One loader serves one file, from one thread.</remarks>
public sealed class FileDataLoader(string path, ReadPolicy policy) : IFileDataLoader, IDisposable
{
    private FileByteSource? source;
    private BoundedReader? reader;
    private FileLayout? layout;
    private TagValues? values;

    public bool Opened => source is not null;

    /// <exception cref="InvalidDataException">The path's extension names no format Goro reads, which discovery never hands over.</exception>
    public FileLayout Layout => layout ??= AudioFormats.Analyse(Format(), Reader());

    public TagValues Values => values ??= new TagValues(Reader());

    public void Dispose() => source?.Dispose();

    private AudioFormat Format() => CandidateFiles.FormatOf(path) ?? throw new InvalidDataException("not a format Goro reads");

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
