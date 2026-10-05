using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Evaluation.Support;

/// <summary>
/// Strings ordered ordinally and prepared not at all: a stand-in for the normalized and literal
/// orders, whose preparation is not the evaluator's to test.
/// </summary>
internal sealed class OrdinalOrder : DatumOrder<string>
{
    public static OrdinalOrder Instance { get; } = new();

    public override int Compare(string x, string y) => string.CompareOrdinal(x, y);
}

/// <summary>
/// An ordinal order whose preparation upper-cases, so that a test can tell prepared data from
/// unprepared, and which counts how many data it prepared.
/// </summary>
internal sealed class UpperCasingOrder : DatumOrder<string>
{
    public List<string> Prepared { get; } = [];

    public override string Prepare(string datum)
    {
        Prepared.Add(datum);
        return datum.ToUpperInvariant();
    }

    public override int Compare(string x, string y) => string.CompareOrdinal(x, y);
}
