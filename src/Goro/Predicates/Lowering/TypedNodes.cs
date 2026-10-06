using System.Diagnostics;
using Goro.Predicates.Binding;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Lowering;

/// <summary>
/// The bridge from Goro's types, which static analysis knows only as values of <see cref="GoroType"/>,
/// to the C# type parameters of the evaluation nodes. This is the one place where that happens.
/// </summary>
/// <remarks>
/// Where a node is generic over its operand's datum type, <see cref="Expression.Apply{TResult}"/>
/// hands the operand back with its type as a type argument, and every other operand of the same
/// node is cast to match; the binder has checked that the types agree before lowering asks. Where both
/// types are concrete, as for literals and conversions, a switch says it directly.
/// </remarks>
internal static class TypedNodes
{
    /// <summary>A literal, standing for what the binder decided it stands for.</summary>
    public static Expression Literal(SemanticLiteral literal) =>
        literal.StandsIn ? UnitLiteral(literal.Type!.Value, ((NumberToken)literal.Token).Value) : Literal(literal.Token);

    public static Expression Literal(Token literal) => literal switch
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
    public static Expression UnitLiteral(GoroType type, decimal value) => type switch
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
    public static Expression Number(Expression argument, Origin origin) => argument.Type switch
    {
        GoroType.Number => argument,
        GoroType.String => new Conversion<string, decimal>((Expression<string>)argument, Conversions.NumberFromString, origin),
        GoroType.ByteCount => new Conversion<ByteCount, decimal>((Expression<ByteCount>)argument, Conversions.NumberFromByteCount, origin),
        GoroType.Duration => new Conversion<Duration, decimal>((Expression<Duration>)argument, Conversions.NumberFromDuration, origin),
        var type => throw new UnreachableException($"NUMBER() does not accept a {type}."),
    };

    /// <summary><c>STRING(argument)</c>; a string is returned as it is.</summary>
    public static Expression String(Expression argument, Origin origin) => argument.Type switch
    {
        GoroType.String => argument,
        GoroType.Number => new Conversion<decimal, string>((Expression<decimal>)argument, Conversions.StringFromNumber, origin),
        GoroType.ByteCount => new Conversion<ByteCount, string>((Expression<ByteCount>)argument, Conversions.StringFromByteCount, origin),
        GoroType.Duration => new Conversion<Duration, string>((Expression<Duration>)argument, Conversions.StringFromDuration, origin),
        var type => throw new UnreachableException($"STRING() does not accept a {type}."),
    };

    public static Expression Count(Expression argument) => argument.Apply(CountOf.Instance);

    /// <param name="default">A literal of the argument's type.</param>
    public static Expression Fallback(Expression argument, Expression @default) =>
        argument.Apply(new FallbackOf(@default));

    public static Condition Comparison(
        Expression left, Quantifier leftQuantifier, ComparisonOperator @operator,
        Expression right, Quantifier rightQuantifier, ComparisonMode mode) =>
        left.Apply(new ComparisonOf(leftQuantifier, @operator, right, rightQuantifier, mode));

    public static Condition StateTest(Expression operand, Quantifier quantifier, TestedState state) =>
        operand.Apply(new StateTestOf(quantifier, state));

    /// <summary>
    /// The two ends of a range, both literals of one type, prepared by the order the operator will
    /// use: for strings, normalized or not as its mode says. Each is prepared exactly once, here.
    /// </summary>
    public static PreparedRange PrepareRange(Expression minimum, Expression maximum, ComparisonMode mode) =>
        minimum.Apply(new RangeOf(maximum, mode));

    private sealed class CountOf : IExpressionFunc<Expression>
    {
        public static CountOf Instance { get; } = new();

        public Expression Invoke<T>(Expression<T> expression) where T : notnull => new Count<T>(expression);
    }

    private sealed class FallbackOf(Expression @default) : IExpressionFunc<Expression>
    {
        public Expression Invoke<T>(Expression<T> expression) where T : notnull =>
            new Fallback<T>(expression, ((Literal<T>)@default).Value);
    }

    private sealed class ComparisonOf(
        Quantifier leftQuantifier, ComparisonOperator @operator, Expression right, Quantifier rightQuantifier, ComparisonMode mode)
        : IExpressionFunc<Condition>
    {
        public Condition Invoke<T>(Expression<T> left) where T : notnull =>
            new ComparisonTest<T>(new(left, leftQuantifier), @operator, new((Expression<T>)right, rightQuantifier), OrderFor<T>(mode));
    }

    private sealed class StateTestOf(Quantifier quantifier, TestedState state) : IExpressionFunc<Condition>
    {
        public Condition Invoke<T>(Expression<T> operand) where T : notnull => new StateTest<T>(operand, quantifier, state);
    }

    private sealed class RangeOf(Expression maximum, ComparisonMode mode) : IExpressionFunc<PreparedRange>
    {
        public PreparedRange Invoke<T>(Expression<T> minimum) where T : notnull
        {
            var order = OrderFor<T>(mode);
            return new PreparedRange<T>(
                order.Prepare(((Literal<T>)minimum).Value), order.Prepare(((Literal<T>)maximum).Value), order);
        }
    }
}

/// <summary>The ends of a range, prepared, waiting for the subject that makes them a range test.</summary>
public abstract class PreparedRange
{
    private protected PreparedRange()
    {
    }

    /// <summary>
    /// Whether the range holds nothing: the minimum, compared with the maximum as a datum would be,
    /// lies above it. For strings, <c>"mi".."m"</c> is not reversed, since every string beginning
    /// with "mi" lies within it.
    /// </summary>
    public abstract bool IsReversed { get; }

    /// <param name="subject">An expression of the range's type.</param>
    public abstract Condition Test(Expression subject, Quantifier quantifier);
}

internal sealed class PreparedRange<T>(T minimum, T maximum, DatumOrder<T> order) : PreparedRange where T : notnull
{
    public override bool IsReversed => order.CompareWithEndpoint(minimum, maximum) > 0;

    public override Condition Test(Expression subject, Quantifier quantifier) =>
        new RangeTest<T>(new((Expression<T>)subject, quantifier), minimum, maximum, order);
}
