namespace Goro.Predicates.Diagnostics;

/// <summary>
/// A stretch of the predicate text, by character offset. Every token and syntax node carries one,
/// so that diagnostics can point at what was written and warnings can quote it.
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    /// <summary>The smallest span containing both <paramref name="first"/> and <paramref name="last"/>.</summary>
    public static TextSpan Covering(TextSpan first, TextSpan last) =>
        FromBounds(Math.Min(first.Start, last.Start), Math.Max(first.End, last.End));

    public string Of(string text) => text.Substring(Start, Length);
}
