using Goro.Predicates.Identifiers;
using Goro.Reading.Bytes;

namespace Goro.Tests.TestSupport;

internal static class TestFiles
{
    /// <summary>
    /// The data of a file whose contents the test does not read: its loader would open the file only
    /// if something inside it were asked for. A test that reads a file's contents owns a
    /// <see cref="FileDataLoader"/> itself, and disposes it.
    /// </summary>
    public static FileData Data(string path) => new(path, new FileDataLoader(path, ReadPolicy.Default));
}
