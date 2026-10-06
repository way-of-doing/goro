using Goro.Messages;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Predicates.Analyses;

/// <summary>The errors of the constants analysis, and the rewrites it offers for them.</summary>
internal sealed class ConstantsDiagnostics(string text) : SemanticDiagnostics(text)
{
    public Diagnostic NegativeUnitLiteral(LiteralSyntax literal, GoroType unit) =>
        new(Codes.NegativeUnitLiteral, literal.Span, new ErrorMessage.NegativeUnitLiteral(Code(literal), UnitOf(unit)));

    public Diagnostic FractionalDurationLiteral(LiteralSyntax literal) =>
        new(Codes.FractionalDurationLiteral, literal.Span, new ErrorMessage.FractionalDurationLiteral(Code(literal)));

    public Diagnostic FallbackDefaultNotConstant(FunctionCallSyntax call) =>
        new(Codes.FallbackDefaultNotConstant, call.Arguments[1].Span, new ErrorMessage.FallbackDefaultNotConstant(new Code(call.Name.Text), Code(call.Arguments[1])));

    public Diagnostic ConstantDoesNotConvert(ExpressionSyntax operand, GoroType target) =>
        new(Codes.ConstantDoesNotConvert, operand.Span, new ErrorMessage.ConstantDoesNotConvert(Code(operand), target));

    public Diagnostic RangeReversed(BetweenSyntax between, GoroType type, ComparisonMode mode)
    {
        var order = type != GoroType.String ? RangeOrder.Plain
            : mode == ComparisonMode.Literal ? RangeOrder.Literal
            : RangeOrder.Normalized;
        var message = new ErrorMessage.RangeReversed(Code(RangeSpan(between)), Code(between.Minimum), Code(between.Maximum), order);
        var swapped = Text(between.Maximum) + Text(TextSpan.FromBounds(between.Minimum.Span.End, between.Maximum.Span.Start)) + Text(between.Minimum);
        return new Diagnostic(Codes.RangeReversed, RangeSpan(between), message,
            [new Suggestion(RangeSpan(between), swapped)]);
    }
}
