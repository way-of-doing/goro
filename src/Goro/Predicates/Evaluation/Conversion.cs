// Owned by the evaluator group (G5) of the predicate-runtime-architecture line. The public
// surface is part of the line's contract; the evaluation bodies are G5's to write.
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>Converts one datum, or fails to; see <see cref="Conversions"/>.</summary>
public delegate bool DatumConversion<in TFrom, TTo>(TFrom datum, [MaybeNullWhen(false)] out TTo converted);

/// <summary>
/// <c>e AS T</c>: converts each usable occurrence, propagates each unusable
/// one with its origin unchanged, and gives an occurrence that fails to convert this call's own
/// <see cref="Origin"/>.
/// </summary>
public sealed class Conversion<TFrom, TTo>(Expression<TFrom> argument, DatumConversion<TFrom, TTo> convert, Origin origin)
    : Expression<TTo> where TFrom : notnull where TTo : notnull
{
    private readonly Unusable<TTo> failure = new(origin);

    public Expression<TFrom> Argument { get; } = argument;

    public DatumConversion<TFrom, TTo> Convert { get; } = convert;

    public Origin Origin { get; } = origin;

    public override Bounds Bounds => Argument.Bounds;

    // Maps each occurrence to one occurrence, so cardinality is kept and absence stays absence. An
    // unusable occurrence is propagated rather than read, keeping its origin, and nothing is reported.
    public override Value<TTo> Evaluate(EvaluationContext context)
    {
        var value = Argument.Evaluate(context);
        return value.IsAbsent ? Value<TTo>.Absent : Value<TTo>.Of(value.Occurrences.Select(ConvertOccurrence));
    }

    private Occurrence<TTo> ConvertOccurrence(Occurrence<TFrom> occurrence) => occurrence switch
    {
        Usable<TFrom>(var datum) => Convert(datum, out var converted) ? new Usable<TTo>(converted) : failure,
        Unusable<TFrom>(var origin) => new Unusable<TTo>(origin),
        _ => throw new UnreachableException(),
    };
}
