using System.Text.RegularExpressions;
using Goro.Predicates.Text;
using Prepared = Goro.Predicates.Text.Normalization;

namespace Goro.Scratchpad.Normalization;

// Goro's own normalization (src/Goro/Predicates/Text), as one more candidate beside the ones it was
// chosen from, so that the smoke tests and the benchmark can run it alongside them.
public sealed class GoroNormalizer : INormalizer
{
    public string Name => "goro";
    public string Normalize(string s) => Prepared.Prepare(s, ComparisonMode.Normalized);
    public string Literal(string s) => Prepared.Prepare(s, ComparisonMode.Literal);
    public int Compare(string a, string b) => StringOrder.Normalized.Compare(a, b);

    public bool Match(string subject, string pattern, bool literally) =>
        new Regex(pattern, RegexOpts.For(literally)).IsMatch(literally ? Literal(subject) : Normalize(subject));
}
