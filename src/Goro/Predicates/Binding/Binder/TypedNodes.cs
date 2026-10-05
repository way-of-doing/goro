// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using System.Diagnostics;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// The bridge from Goro's types, which the binder knows only as values of <see cref="GoroType"/>,
/// to the C# type parameters of the bound nodes. This is the one place where that happens.
/// </summary>
/// <remarks>
/// Where a node is generic over its operand's datum type, <see cref="BoundExpression.Apply{TResult}"/>
/// hands the operand back with its type as a type argument, and every other operand of the same
/// node is cast to match; the binder has checked that the types agree before it asks. Where both
/// types are concrete, as for literals and conversions, a switch says it directly.
/// </remarks>
internal static class TypedNodes
{
    public static GoroType TypeOf(Token literal) => literal switch
    {
        StringToken => GoroType.String,
        NumberToken => GoroType.Number,
        ByteCountToken => GoroType.ByteCount,
        DurationToken => GoroType.Duration,
        { Kind: TokenKind.True or TokenKind.False } => GoroType.Boolean,
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal.Kind, "Not a literal."),
    };

    public static BoundExpression Literal(Token literal) => literal switch
    {
        StringToken text => new Literal<string>(text.Value),
        NumberToken number => new Literal<decimal>(number.Value),
        ByteCountToken bytes => new Literal<ByteCount>(bytes.Value),
        DurationToken duration => new Literal<Duration>(duration.Value),
        { Kind: TokenKind.True } => new Literal<bool>(true),
        { Kind: TokenKind.False } => new Literal<bool>(false),
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal.Kind, "Not a literal."),
    };

    /// <summary>A number literal standing for a bytecount (that many bytes) or a duration (that many seconds).</summary>
    public static BoundExpression UnitLiteral(GoroType type, decimal value) => type switch
    {
        GoroType.ByteCount => new Literal<ByteCount>(new ByteCount(value)),
        GoroType.Duration => new Literal<Duration>(new Duration(value)),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Only a bytecount or a duration has a unit."),
    };

    /// <summary>How an operator in <paramref name="mode"/> orders data of type <typeparamref name="T"/>.</summary>
    public static DatumOrder<T> OrderFor<T>(ComparisonMode mode) where T : notnull => (DatumOrder<T>)(GoroTypes.Of<T>() switch
    {
        GoroType.String => (object)StringOrder.For(mode),
        GoroType.Number => NaturalOrder<decimal>.Instance,
        GoroType.ByteCount => NaturalOrder<ByteCount>.Instance,
        GoroType.Duration => NaturalOrder<Duration>.Instance,
        GoroType.Boolean => NaturalOrder<bool>.Instance,
        var type => throw new UnreachableException($"{type} has no order."),
    });

    /// <summary><c>NUMBER(argument)</c>; a number is returned as it is.</summary>
    public static BoundExpression Number(BoundExpression argument, Origin origin) => argument.Type switch
    {
        GoroType.Number => argument,
        GoroType.String => new Conversion<string, decimal>((BoundExpression<string>)argument, Conversions.NumberFromString, origin),
        GoroType.ByteCount => new Conversion<ByteCount, decimal>((BoundExpression<ByteCount>)argument, Conversions.NumberFromByteCount, origin),
        GoroType.Duration => new Conversion<Duration, decimal>((BoundExpression<Duration>)argument, Conversions.NumberFromDuration, origin),
        var type => throw new UnreachableException($"NUMBER() does not accept a {type}."),
    };

    /// <summary><c>STRING(argument)</c>; a string is returned as it is.</summary>
    public static BoundExpression String(BoundExpression argument, Origin origin) => argument.Type switch
    {
        GoroType.String => argument,
        GoroType.Number => new Conversion<decimal, string>((BoundExpression<decimal>)argument, Conversions.StringFromNumber, origin),
        GoroType.ByteCount => new Conversion<ByteCount, string>((BoundExpression<ByteCount>)argument, Conversions.StringFromByteCount, origin),
        GoroType.Duration => new Conversion<Duration, string>((BoundExpression<Duration>)argument, Conversions.StringFromDuration, origin),
        var type => throw new UnreachableException($"STRING() does not accept a {type}."),
    };

    public static BoundExpression Count(BoundExpression argument) => argument.Apply(CountOf.Instance);

    /// <param name="default">A literal of the argument's type.</param>
    public static BoundExpression Fallback(BoundExpression argument, BoundExpression @default) =>
        argument.Apply(new FallbackOf(@default));

    public static BoundCondition Comparison(
        BoundExpression left, Quantifier leftQuantifier, ComparisonOperator @operator,
        BoundExpression right, Quantifier rightQuantifier, ComparisonMode mode) =>
        left.Apply(new ComparisonOf(leftQuantifier, @operator, right, rightQuantifier, mode));

    public static BoundCondition StateTest(BoundExpression operand, Quantifier quantifier, TestedState state) =>
        operand.Apply(new StateTestOf(quantifier, state));

    /// <summary>
    /// The two ends of a range, both literals of one type, prepared by the order the operator will
    /// use: for strings, normalized or not as its mode says. Each is prepared exactly once, here.
    /// </summary>
    public static PreparedRange PrepareRange(BoundExpression minimum, BoundExpression maximum, ComparisonMode mode) =>
        minimum.Apply(new RangeOf(maximum, mode));

    private sealed class CountOf : IBoundExpressionFunc<BoundExpression>
    {
        public static CountOf Instance { get; } = new();

        public BoundExpression Invoke<T>(BoundExpression<T> expression) where T : notnull => new Count<T>(expression);
    }

    private sealed class FallbackOf(BoundExpression @default) : IBoundExpressionFunc<BoundExpression>
    {
        public BoundExpression Invoke<T>(BoundExpression<T> expression) where T : notnull =>
            new Fallback<T>(expression, ((Literal<T>)@default).Value);
    }

    private sealed class ComparisonOf(
        Quantifier leftQuantifier, ComparisonOperator @operator, BoundExpression right, Quantifier rightQuantifier, ComparisonMode mode)
        : IBoundExpressionFunc<BoundCondition>
    {
        public BoundCondition Invoke<T>(BoundExpression<T> left) where T : notnull =>
            new ComparisonTest<T>(new(left, leftQuantifier), @operator, new((BoundExpression<T>)right, rightQuantifier), OrderFor<T>(mode));
    }

    private sealed class StateTestOf(Quantifier quantifier, TestedState state) : IBoundExpressionFunc<BoundCondition>
    {
        public BoundCondition Invoke<T>(BoundExpression<T> operand) where T : notnull => new StateTest<T>(operand, quantifier, state);
    }

    private sealed class RangeOf(BoundExpression maximum, ComparisonMode mode) : IBoundExpressionFunc<PreparedRange>
    {
        public PreparedRange Invoke<T>(BoundExpression<T> minimum) where T : notnull
        {
            var order = OrderFor<T>(mode);
            return new PreparedRange<T>(
                order.Prepare(((Literal<T>)minimum).Value), order.Prepare(((Literal<T>)maximum).Value), order);
        }
    }
}

/// <summary>The ends of a range, prepared, waiting for the subject that makes them a range test.</summary>
internal abstract class PreparedRange
{
    /// <summary>
    /// Whether the range holds nothing: the minimum, compared with the maximum as a datum would be,
    /// lies above it. For strings, <c>"mi".."m"</c> is not reversed, since every string beginning
    /// with "mi" lies within it.
    /// </summary>
    public abstract bool IsReversed { get; }

    /// <param name="subject">An expression of the range's type.</param>
    public abstract BoundCondition Test(BoundExpression subject, Quantifier quantifier);
}

internal sealed class PreparedRange<T>(T minimum, T maximum, DatumOrder<T> order) : PreparedRange where T : notnull
{
    public override bool IsReversed => order.CompareWithEndpoint(minimum, maximum) > 0;

    public override BoundCondition Test(BoundExpression subject, Quantifier quantifier) =>
        new RangeTest<T>(new((BoundExpression<T>)subject, quantifier), minimum, maximum, order);
}
