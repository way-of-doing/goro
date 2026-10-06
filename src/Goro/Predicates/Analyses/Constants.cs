using System.Collections.Immutable;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Lowering;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Analyses;

/// <summary>
/// Literal values, and the rules about them: a number literal standing for a bytecount must not be
/// negative, and one standing for a duration must also be whole; the default of <c>FALLBACK()</c>
/// must be a literal; <c>NUMBER()</c> rejects a string literal that is not a number; and the ends of
/// a range must not be reversed, compared as the operator will compare them.
/// </summary>
/// <remarks>
/// Comparing the ends of a range as the operator will means preparing them as it will, so this is
/// where every range's ends are prepared, once, and lowering takes them from here.
/// </remarks>
public static class Constants
{
    public static ConstantsAnalysis Analyse(SemanticTree tree)
    {
        var report = new ConstantsDiagnostics(tree.Text);
        var diagnostics = new List<Diagnostic>();
        var ranges = new Dictionary<SemanticRange, PreparedRange>();
        foreach (var node in tree.Nodes)
        {
            switch (node)
            {
                case SemanticLiteral literal when Fit(literal) is not UnitFit.Fits:
                    diagnostics.Add(Fit(literal) == UnitFit.Negative
                        ? report.NegativeUnitLiteral(literal.Syntax, literal.Type!.Value)
                        : report.FractionalDurationLiteral(literal.Syntax));
                    break;

                case SemanticConversion { Type: GoroType.Number, Argument: SemanticLiteral { Token: StringToken text } literal } conversion
                    when !NumberText.TryParse(text.Value, out _):
                    diagnostics.Add(report.InvalidNumberLiteral(conversion.Syntax, literal.Syntax));
                    break;

                case SemanticFallback { Default: { IsError: false } and not SemanticLiteral } fallback:
                    diagnostics.Add(report.FallbackDefaultNotLiteral(fallback.Syntax));
                    break;

                case SemanticRange range when Prepare(range) is { } prepared:
                    if (prepared.IsReversed)
                    {
                        diagnostics.Add(report.RangeReversed(range.Syntax, range.Minimum.Type!.Value, range.Mode));
                    }
                    else
                    {
                        ranges.Add(range, prepared);
                    }

                    break;
            }
        }

        return new ConstantsAnalysis(ranges, [.. diagnostics]);
    }

    /// <summary>
    /// The ends of a range, prepared in its operator's mode; null where they cannot be compared, the
    /// binder having found they disagree in type or are booleans, or an end having a value it cannot
    /// stand for, which is reported on its own.
    /// </summary>
    private static PreparedRange? Prepare(SemanticRange range)
    {
        var (minimum, maximum) = (range.Minimum, range.Maximum);
        if (minimum.Type != maximum.Type || minimum.Type == GoroType.Boolean || !HoldsItsValue(minimum) || !HoldsItsValue(maximum))
        {
            return null;
        }

        return TypedNodes.PrepareRange(TypedNodes.Literal(minimum), TypedNodes.Literal(maximum), range.Mode);
    }

    private static bool HoldsItsValue(SemanticLiteral literal) => Fit(literal) == UnitFit.Fits;

    /// <summary>
    /// Whether a literal can be what it stands for: a number literal standing for a bytecount must
    /// not be negative, and one standing for a duration must also be whole. Every other literal fits.
    /// </summary>
    private static UnitFit Fit(SemanticLiteral literal)
    {
        if (!literal.StandsIn)
        {
            return UnitFit.Fits;
        }

        var value = ((NumberToken)literal.Token).Value;
        return value < 0 ? UnitFit.Negative
            : literal.Type == GoroType.Duration && value != decimal.Truncate(value) ? UnitFit.Fractional
            : UnitFit.Fits;
    }

    private enum UnitFit
    {
        Fits,
        Negative,
        Fractional,
    }
}

/// <summary>The prepared ends of every sound range of a semantic tree, and the errors about literal values.</summary>
public sealed class ConstantsAnalysis
{
    private readonly IReadOnlyDictionary<SemanticRange, PreparedRange> ranges;

    internal ConstantsAnalysis(IReadOnlyDictionary<SemanticRange, PreparedRange> ranges, ImmutableArray<Diagnostic> diagnostics)
    {
        this.ranges = ranges;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>The ends of <paramref name="range"/>, prepared, if they were sound and in order.</summary>
    public PreparedRange? RangeOf(SemanticRange range) => ranges.GetValueOrDefault(range);
}
