using System.Collections.Immutable;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Evaluation.Support;

/// <summary>
/// An identifier binding that resolves to the same bag for every file, its unusable occurrences
/// carrying the origin of the reference being resolved, as every binding's do. It counts how often
/// it is resolved, which is how a test sees whether an operand was evaluated at all.
/// </summary>
internal sealed class CannedBinding<T>(ImmutableArray<Occurrence<T>> occurrences) : IdentifierBinding<T>
    where T : notnull
{
    public int Resolutions { get; private set; }

    public override Value<T> Resolve(FileData file, Origin origin)
    {
        Resolutions++;
        return Value<T>.Of(occurrences.Select(o => o is Unusable<T> ? new Unusable<T>(origin) : o));
    }
}

/// <summary>A binding whose data cannot be read, as a tag namespace's is for a file with corrupt tags.</summary>
internal sealed class UnreadableBinding<T> : IdentifierBinding<T> where T : notnull
{
    public override Value<T> Resolve(FileData file, Origin origin) =>
        throw new UnreadableFileException(file.Path, "the tags cannot be parsed");
}
