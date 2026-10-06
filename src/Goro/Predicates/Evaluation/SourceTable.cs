using System.Collections.Immutable;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// Every distinct warning source a predicate can report, each described in a canonical form that
/// ignores how it was written, such as <c>vorbis::raw::bpm AS NUMBER</c>. A <see cref="SourceId"/> is
/// an index into this table.
/// </summary>
public sealed class SourceTable(ImmutableArray<string> canonicalForms)
{
    public static SourceTable Empty { get; } = new([]);

    public int Count => canonicalForms.Length;

    public string this[SourceId id] => canonicalForms[id.Value];

    public ImmutableArray<string> CanonicalForms => canonicalForms;
}
