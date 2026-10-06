namespace Goro.Predicates.Values;

/// <summary>
/// The cardinality bounds of an expression, known when the predicate is read: whether it can be
/// absent for some file (a lower bound of 0 rather than 1), and whether it can hold more than one
/// occurrence (an upper bound of many rather than 1). See the static judgements of
/// docs/concepts/evaluation.md.
/// </summary>
public readonly record struct Bounds(bool CanBeAbsent, bool CanBeSeveral)
{
    /// <summary><c>1..1</c>: a literal, an operator, <c>COUNT()</c>, <c>file::size</c>.</summary>
    public static Bounds ExactlyOne { get; } = new(CanBeAbsent: false, CanBeSeveral: false);

    /// <summary><c>0..1</c>: an Id3v1 field, <c>file::extension</c>.</summary>
    public static Bounds AtMostOne { get; } = new(CanBeAbsent: true, CanBeSeveral: false);

    /// <summary><c>0..many</c>: a field of a tag that may repeat it.</summary>
    public static Bounds Any { get; } = new(CanBeAbsent: true, CanBeSeveral: true);

    /// <summary>The bounds of <c>FALLBACK()</c> of an expression with these: never absent, and no fewer occurrences.</summary>
    public Bounds Fallback() => this with { CanBeAbsent = false };

    /// <summary>
    /// The bounds of <c>PREFERRED()</c> of arguments with these: never absent if some argument never is,
    /// and holding several if any argument can, the result being one of them.
    /// </summary>
    public static Bounds Preferred(IEnumerable<Bounds> arguments)
    {
        var all = arguments.ToList();
        return new(all.All(bounds => bounds.CanBeAbsent), all.Any(bounds => bounds.CanBeSeveral));
    }

    public override string ToString() => $"{(CanBeAbsent ? 0 : 1)}..{(CanBeSeveral ? "many" : "1")}";
}
