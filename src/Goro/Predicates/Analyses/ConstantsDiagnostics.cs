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

    public Diagnostic FallbackDefaultNotLiteral(FunctionCallSyntax call) =>
        new(Codes.FallbackDefaultNotLiteral, call.Arguments[1].Span, new ErrorMessage.FallbackDefaultNotLiteral(new Code(call.Name.Text), Code(call.Arguments[1])));

    public Diagnostic InvalidNumberLiteral(FunctionCallSyntax call, LiteralSyntax literal) =>
        new(Codes.InvalidNumberLiteral, literal.Span, new ErrorMessage.InvalidNumberLiteral(Code(literal), new Code(call.Name.Text)));

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
