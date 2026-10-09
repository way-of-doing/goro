using System.Collections.Immutable;
using Goro.Discovery;
using Goro.Predicates.Values;
using Goro.Reading;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The <c>file</c> source: the properties of the file itself rather than of its tags. Each concept
/// reads only what it needs -- <c>path</c>, <c>name</c> and <c>extension</c> nothing at all,
/// <c>size</c> the file system's metadata, <c>duration</c> the analysis of the file's edges -- which
/// is what lets a predicate that asks only for the cheap ones run over files that cannot be read.
/// Data that cannot be had makes the file unreadable, except a playing time that cannot be had in a
/// file whose audio was found, which is an unusable <c>file::duration</c>.
/// </summary>
internal static class FileSource
{
    public static ImmutableArray<IdentifierDeclaration> Concepts { get; } =
    [
        Concept("duration", Bounds.ExactlyOne, (file, origin) => file.Get(TagsFacet.Instance).Layout.Duration is DurationOutcome.Known known
            ? One(new Duration(known.WholeSeconds))
            : Value<Duration>.Single(new Unusable<Duration>(origin))),
        Concept("extension", Bounds.AtMostOne, (file, _) => CandidateFiles.Extension(NameOf(file)) is { } extension ? One(extension) : Value<string>.Absent),
        Concept("name", Bounds.ExactlyOne, (file, _) => One(NameOf(file))),
        Concept("path", Bounds.ExactlyOne, (file, _) => One(FilePaths.ToPredicatePath(file.Path))),
        Concept("size", Bounds.ExactlyOne, (file, _) => One(new ByteCount(file.Get(FileSystemFacet.Instance).Length))),
    ];

    private static string NameOf(FileData file) => Path.GetFileName(file.Path);

    private static Value<T> One<T>(T datum) where T : notnull => Value<T>.Single(new Usable<T>(datum));

    private static IdentifierDeclaration<T> Concept<T>(string name, Bounds bounds, Func<FileData, Origin, Value<T>> resolve) where T : notnull =>
        new(new IdentifierName(SourceNames.File, name), bounds, new Binding<T>(resolve));

    private sealed class Binding<T>(Func<FileData, Origin, Value<T>> resolve) : IdentifierBinding<T> where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin) => resolve(file, origin);
    }
}
