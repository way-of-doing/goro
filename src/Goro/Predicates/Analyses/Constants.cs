using System.Collections.Immutable;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Analyses;

/// <summary>
/// Values known when the predicate is read, and the rules about them: a number literal standing for
/// a bytecount must not be negative, and one standing for a duration must also be whole; the default
/// of <c>FALLBACK()</c> must be a constant; a conversion of a constant must succeed; and the ends of a
/// range must not be reversed, compared as the operator will compare them.
/// </summary>
/// <remarks>
/// Comparing the ends of a range as the operator will means preparing them as it will, so this is
/// where every range's ends are prepared, once, and lowering takes them from here. Likewise every
/// conversion of a constant is worked out here, once, which is how one that fails is found, and
/// lowering takes the literal it gives instead of converting for every file.
/// </remarks>
public static class Constants
{
    public static ConstantsAnalysis Analyse(SemanticTree tree)
    {
        var report = new ConstantsDiagnostics(tree.Text);
        var diagnostics = new List<Diagnostic>();
        var ranges = new Dictionary<SemanticRange, PreparedRange>();
        var values = new Dictionary<SemanticExpression, Expression>();
        foreach (var node in tree.Nodes)
        {
            // Children come before their parents, so a constant's argument has its value by now.
            if (Value(node, values) is { } value)
            {
                values.Add(node, value);
            }

            switch (node)
            {
                case SemanticLiteral literal when Fit(literal) is not UnitFit.Fits:
                    diagnostics.Add(Fit(literal) == UnitFit.Negative
                        ? report.NegativeUnitLiteral(literal.Syntax, literal.Type!.Value)
                        : report.FractionalDurationLiteral(literal.Syntax));
                    break;

                // A string constant that does not convert to a number. No other conversion can fail.
                case SemanticConversion { Type: GoroType.Number, IsConstant: true } conversion
                    when !values.ContainsKey(conversion) && values.GetValueOrDefault(conversion.Argument) is Literal<string>:
                    diagnostics.Add(report.InvalidNumberLiteral(conversion.Syntax, conversion.Argument.Syntax));
                    break;

                case SemanticFallback { Default: { IsError: false, IsConstant: false } } fallback:
                    diagnostics.Add(report.FallbackDefaultNotConstant(fallback.Syntax));
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

        return new ConstantsAnalysis(ranges, values, [.. diagnostics]);
    }

    /// <summary>
    /// The value of a constant, as the literal lowering is to use for it; null for anything that is not
    /// a constant, or a conversion that fails, or that the binder found mistyped, which is reported
    /// already.
    /// </summary>
    private static Expression? Value(SemanticExpression node, Dictionary<SemanticExpression, Expression> values) => node switch
    {
        SemanticLiteral { IsError: false } literal when HoldsItsValue(literal) => TypedNodes.Literal(literal.Token, literal.Type!.Value),
        SemanticConversion { IsConstant: true, IsError: false } conversion
            when values.GetValueOrDefault(conversion.Argument) is { Type: not GoroType.Boolean } argument =>
            TypedNodes.ConvertLiteral(argument, conversion.Type!.Value),
        _ => null,
    };

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

        return TypedNodes.PrepareRange(
            TypedNodes.Literal(minimum.Token, minimum.Type!.Value), TypedNodes.Literal(maximum.Token, maximum.Type!.Value), range.Mode);
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

    private readonly IReadOnlyDictionary<SemanticExpression, Expression> values;

    internal ConstantsAnalysis(
        IReadOnlyDictionary<SemanticRange, PreparedRange> ranges,
        IReadOnlyDictionary<SemanticExpression, Expression> values,
        ImmutableArray<Diagnostic> diagnostics)
    {
        this.ranges = ranges;
        this.values = values;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>The value of a constant, worked out when the predicate was read: a literal of its type.</summary>
    public Expression? ValueOf(SemanticExpression constant) => values.GetValueOrDefault(constant);

    /// <summary>The ends of <paramref name="range"/>, prepared, if they were sound and in order.</summary>
    public PreparedRange? RangeOf(SemanticRange range) => ranges.GetValueOrDefault(range);
}
