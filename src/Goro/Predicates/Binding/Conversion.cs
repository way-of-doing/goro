// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using System.Diagnostics.CodeAnalysis;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>Converts one datum, or fails to; see <see cref="Conversions"/>.</summary>
public delegate bool DatumConversion<in TFrom, TTo>(TFrom datum, [MaybeNullWhen(false)] out TTo converted);

/// <summary>
/// <c>NUMBER(e)</c> or <c>STRING(e)</c>: converts each usable occurrence, propagates each unusable
/// one with its origin unchanged, and gives an occurrence that fails to convert this call's own
/// <see cref="Origin"/>.
/// </summary>
public sealed class Conversion<TFrom, TTo>(BoundExpression<TFrom> argument, DatumConversion<TFrom, TTo> convert, Origin origin)
    : BoundExpression<TTo> where TFrom : notnull where TTo : notnull
{
    public BoundExpression<TFrom> Argument { get; } = argument;

    public DatumConversion<TFrom, TTo> Convert { get; } = convert;

    public Origin Origin { get; } = origin;

    public override bool IsDefinite => Argument.IsDefinite;

    public override Value<TTo> Evaluate(EvaluationContext context) => throw new NotImplementedException();
}
