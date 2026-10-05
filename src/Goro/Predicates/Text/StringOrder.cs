// Owned by the normalization group (G1) of the predicate-runtime-architecture line.
using Goro.Predicates.Values;

namespace Goro.Predicates.Text;

/// <summary>
/// The order of strings in one comparison mode: prepared as that mode says, then compared code
/// point by code point, a prefix sorting first.
/// </summary>
public sealed class StringOrder : DatumOrder<string>
{
    public static StringOrder Normalized { get; } = new(ComparisonMode.Normalized);

    public static StringOrder Literal { get; } = new(ComparisonMode.Literal);

    private StringOrder(ComparisonMode mode) => Mode = mode;

    public ComparisonMode Mode { get; }

    public static StringOrder For(ComparisonMode mode) => mode == ComparisonMode.Literal ? Literal : Normalized;

    public override string Prepare(string datum) => Normalization.Prepare(datum, Mode);

    public override int Compare(string x, string y) => throw new NotImplementedException();
}
