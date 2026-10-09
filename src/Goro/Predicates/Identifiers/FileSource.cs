using System.Collections.Immutable;
using Goro.Discovery;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The <c>file</c> source: the properties of the file itself rather than of its tags. Each concept
/// reads only what it needs -- <c>path</c>, <c>name</c> and <c>extension</c> nothing at all,
/// <c>size</c> the file system's metadata, <c>duration</c> the audio properties -- which is what
/// lets a predicate that asks only for the cheap ones run over files that cannot be read. None of
/// them is ever unusable: data that cannot be had makes the file unreadable instead.
/// </summary>
internal static class FileSource
{

    public static ImmutableArray<IdentifierDeclaration> Concepts { get; } =
    [
        Concept("duration", Bounds.ExactlyOne, file => One(WholeSeconds(file.Get(AudioPropertiesFacet.Instance).Duration))),
        Concept("extension", Bounds.AtMostOne, file => CandidateFiles.Extension(NameOf(file)) is { } extension ? One(extension) : Value<string>.Absent),
        Concept("name", Bounds.ExactlyOne, file => One(NameOf(file))),
        Concept("path", Bounds.ExactlyOne, file => One(FilePaths.ToPredicatePath(file.Path))),
        Concept("size", Bounds.ExactlyOne, file => One(new ByteCount(file.Get(FileSystemFacet.Instance).Length))),
    ];

    private static string NameOf(FileData file) => Path.GetFileName(file.Path);

    private static Duration WholeSeconds(TimeSpan duration) => new(duration.Ticks / TimeSpan.TicksPerSecond);

    private static Value<T> One<T>(T datum) where T : notnull => Value<T>.Single(new Usable<T>(datum));

    private static IdentifierDeclaration<T> Concept<T>(string name, Bounds bounds, Func<FileData, Value<T>> resolve) where T : notnull =>
        new(new IdentifierName(SourceNames.File, name), bounds, new Binding<T>(resolve));

    // A file concept never produces an unusable occurrence, so the origin goes unused.
    private sealed class Binding<T>(Func<FileData, Value<T>> resolve) : IdentifierBinding<T> where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin) => resolve(file);
    }
}
