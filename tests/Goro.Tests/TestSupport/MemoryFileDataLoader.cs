using Goro.Predicates.Identifiers;
using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Reading.Tags;

namespace Goro.Tests.TestSupport;

/// <summary>A file's contents from memory, read as <see cref="FileDataLoader"/> reads a file: for tests that build a file by hand.</summary>
internal sealed class MemoryFileDataLoader(byte[] bytes) : IFileDataLoader
{
    private readonly BoundedReader reader = new(new MemoryByteSource(bytes), ReadPolicy.Default);
    private FileLayout? layout;

    public bool Opened { get; private set; }

    public FileLayout Layout
    {
        get
        {
            Opened = true;
            return layout ??= Mp3Analysis.Analyse(reader);
        }
    }

    public TagValues Values => new(reader);

    /// <summary>Every read made of the file, for asserting what was and was not read.</summary>
    public ReadLog Reads => reader.Log;

    /// <summary>A file of these parts in order, with audio frames after the first.</summary>
    public static MemoryFileDataLoader Mp3(byte[] leading, params byte[][] trailing) =>
        new([.. leading, .. SyntheticMp3Builder.BuildAudioFrames(), .. trailing.SelectMany(part => part)]);
}
