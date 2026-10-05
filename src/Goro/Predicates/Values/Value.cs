using System.Collections.Immutable;

namespace Goro.Predicates.Values;

/// <summary>
/// A value: absent, or a bag of one or more occurrences. There is exactly one representation of
/// absence -- a bag of no occurrences is absent, and so is <c>default</c> -- so the empty bag that
/// the specification says does not exist cannot be constructed either.
/// </summary>
public readonly struct Value<T> where T : notnull
{
    private readonly ImmutableArray<Occurrence<T>> occurrences;

    private Value(ImmutableArray<Occurrence<T>> occurrences) => this.occurrences = occurrences;

    public static Value<T> Absent => default;

    public static Value<T> Of(params ReadOnlySpan<Occurrence<T>> occurrences) =>
        occurrences.IsEmpty ? Absent : new([.. occurrences]);

    public static Value<T> Of(IEnumerable<Occurrence<T>> occurrences) => Of([.. occurrences]);

    public static Value<T> Single(Occurrence<T> occurrence) => new([occurrence]);

    public bool IsAbsent => occurrences.IsDefaultOrEmpty;

    /// <summary>The bag, in no particular order; empty when the value is absent.</summary>
    public ImmutableArray<Occurrence<T>> Occurrences => IsAbsent ? [] : occurrences;

    public override string ToString() => IsAbsent ? "absent" : $"{{{string.Join(", ", occurrences)}}}";
}
