using System.Collections.Immutable;
using Goro.Discovery;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The <c>file</c> namespace: the properties of the file itself rather than of its tags. Each
/// identifier reads only what it needs -- <c>path</c>, <c>name</c> and <c>extension</c> nothing at
/// all, <c>size</c> the file system's metadata, <c>duration</c> the audio properties -- which is
/// what lets a predicate that asks only for the cheap ones run over files that cannot be read.
/// None of them is ever unusable: data that cannot be had makes the file unreadable instead.
/// </summary>
internal static class FileNamespace
{
    public static ImmutableArray<DeclarationRow> Identifiers { get; } =
    [
        Row("duration", isDefinite: false, file => One(WholeSeconds(file.Get(AudioPropertiesFacet.Instance).Duration))),
        Row("extension", isDefinite: false, file => CandidateFiles.Extension(NameOf(file)) is { } extension ? One(extension) : Value<string>.Absent),
        Row("name", isDefinite: true, file => One(NameOf(file))),
        Row("path", isDefinite: true, file => One(FilePaths.ToPredicatePath(file.Path))),
        Row("size", isDefinite: true, file => One(new ByteCount(file.Get(FileSystemFacet.Instance).Length))),
    ];

    private static string NameOf(FileData file) => Path.GetFileName(file.Path);

    private static Duration WholeSeconds(TimeSpan duration) => new(duration.Ticks / TimeSpan.TicksPerSecond);

    private static Value<T> One<T>(T datum) where T : notnull => Value<T>.Single(new Usable<T>(datum));

    private static DeclarationRow<T> Row<T>(string name, bool isDefinite, Func<FileData, Value<T>> resolve) where T : notnull
    {
        var binding = new Binding<T>(resolve);
        return new DeclarationRow<T>(name, isDefinite, _ => binding);
    }

    // A file identifier never produces an unusable occurrence, so the origin goes unused.
    private sealed class Binding<T>(Func<FileData, Value<T>> resolve) : IdentifierBinding<T> where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin) => resolve(file);
    }
}
