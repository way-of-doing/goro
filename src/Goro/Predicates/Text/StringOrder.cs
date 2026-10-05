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

    /// <summary>
    /// Compares two prepared strings by code point. UTF-16 code unit order differs from it only
    /// above U+FFFF, whose surrogates sort below U+E000–U+FFFF although the code points they
    /// encode sort above them.
    /// </summary>
    /// <remarks>Total over any strings, well-formed or not; it is code point order on the well-formed ones.</remarks>
    public override int Compare(string x, string y) => Compare(x.AsSpan(), y.AsSpan());

    /// <summary>
    /// Compares the datum cut to the endpoint's length, so that <c>"mi"</c> as a maximum admits
    /// "miles". Cutting by UTF-16 code units cuts by code points wherever it matters: an endpoint is
    /// a literal, which holds no lone surrogate, so a datum that agrees with it up to the cut holds
    /// whole code points there, and one that does not is decided where it first differs.
    /// </summary>
    public override int CompareWithEndpoint(string datum, string endpoint) =>
        Compare(datum.AsSpan(0, Math.Min(datum.Length, endpoint.Length)), endpoint.AsSpan());

    private static int Compare(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        int common = x.CommonPrefixLength(y);
        if (common == x.Length || common == y.Length) return x.Length.CompareTo(y.Length);

        return CodePointOrderKey(x[common]).CompareTo(CodePointOrderKey(y[common]));
    }

    /// <summary>
    /// Moves surrogates above U+E000–U+FFFF. Where two strings first differ, either both code units
    /// are surrogates of the same kind, ordered among themselves as their code points are, or at
    /// most one is, and then it starts a code point above every one in the BMP.
    /// </summary>
    private static int CodePointOrderKey(char c) => c switch
    {
        >= '\uE000' => c - 0x800,
        >= '\uD800' => c + 0x2000,
        _ => c,
    };
}
