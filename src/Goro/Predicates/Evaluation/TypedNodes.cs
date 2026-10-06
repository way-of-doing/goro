using System.Collections.Immutable;
using System.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// The bridge from Goro's types, which static analysis knows only as values of <see cref="GoroType"/>,
/// to the C# type parameters of the evaluation nodes. This is the one place where that happens.
/// </summary>
/// <remarks>
/// Where a node is generic over its operand's datum type, <see cref="Expression.Apply{TResult}"/>
/// hands the operand back with its type as a type argument, and every other operand of the same
/// node is cast to match; the binder has checked that the types agree before anything asks. Where both
/// types are concrete, as for literals, conversions and identifier references, a switch says it directly.
/// </remarks>
internal static class TypedNodes
{
    /// <summary>A reference to an identifier the binder looked up, typed by its declaration.</summary>
    public static Expression Reference(IdentifierDeclaration declaration, Origin origin) => declaration switch
    {
        IdentifierDeclaration<string> typed => new IdentifierReference<string>(typed, origin),
        IdentifierDeclaration<decimal> typed => new IdentifierReference<decimal>(typed, origin),
        IdentifierDeclaration<ByteCount> typed => new IdentifierReference<ByteCount>(typed, origin),
        IdentifierDeclaration<Duration> typed => new IdentifierReference<Duration>(typed, origin),
        IdentifierDeclaration<bool> typed => new IdentifierReference<bool>(typed, origin),
        _ => throw new UnreachableException($"{declaration.Type} has no datum type."),
    };

    /// <summary>
    /// A literal of <paramref name="type"/>: its token's own type, or a bytecount or a duration that a
    /// number literal stands for, by the numeric literal exception.
    /// </summary>
    public static Expression Literal(Token literal, GoroType type) =>
        literal is NumberToken number && type is GoroType.ByteCount or GoroType.Duration
            ? UnitLiteral(type, number.Value)
            : Literal(literal);

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

    /// <summary>
    /// <c>argument AS target</c>; a conversion to the type the argument already has is the argument.
    /// The binder has rejected every pair of types that does not convert.
    /// </summary>
    public static Expression Convert(Expression argument, GoroType target, Origin origin) =>
        argument.Type == target ? argument : PairFor(argument.Type, target).Node(argument, origin);

    /// <summary>
    /// A conversion of a literal, worked out now: the literal it converts to, or null where it does not
    /// convert. A conversion to the literal's own type is the literal itself.
    /// </summary>
    public static Expression? ConvertLiteral(Expression literal, GoroType target) =>
        literal.Type == target ? literal : PairFor(literal.Type, target).Fold(literal);

    private static ConversionPair PairFor(GoroType from, GoroType to) => (from, to) switch
    {
        (GoroType.String, GoroType.Number) => new ConversionPair<string, decimal>(Conversions.NumberFromString),
        (GoroType.String, GoroType.Duration) => new ConversionPair<string, Duration>(Conversions.DurationFromString),
        (GoroType.String, GoroType.ByteCount) => new ConversionPair<string, ByteCount>(Conversions.ByteCountFromString),
        (GoroType.Number, GoroType.String) => new ConversionPair<decimal, string>(Conversions.StringFromNumber),
        (GoroType.Number, GoroType.Duration) => new ConversionPair<decimal, Duration>(Conversions.DurationFromNumber),
        (GoroType.Number, GoroType.ByteCount) => new ConversionPair<decimal, ByteCount>(Conversions.ByteCountFromNumber),
        (GoroType.Duration, GoroType.String) => new ConversionPair<Duration, string>(Conversions.StringFromDuration),
        (GoroType.Duration, GoroType.Number) => new ConversionPair<Duration, decimal>(Conversions.NumberFromDuration),
        (GoroType.ByteCount, GoroType.String) => new ConversionPair<ByteCount, string>(Conversions.StringFromByteCount),
        (GoroType.ByteCount, GoroType.Number) => new ConversionPair<ByteCount, decimal>(Conversions.NumberFromByteCount),
        _ => throw new UnreachableException($"A {from} does not convert to a {to}."),
    };

    private abstract class ConversionPair
    {
        public abstract Expression Node(Expression argument, Origin origin);

        public abstract Expression? Fold(Expression literal);
    }

    private sealed class ConversionPair<TFrom, TTo>(DatumConversion<TFrom, TTo> convert) : ConversionPair
        where TFrom : notnull where TTo : notnull
    {
        public override Expression Node(Expression argument, Origin origin) =>
            new Conversion<TFrom, TTo>((Expression<TFrom>)argument, convert, origin);

        public override Expression? Fold(Expression literal) =>
            convert(((Literal<TFrom>)literal).Value, out var converted) ? new Literal<TTo>(converted) : null;
    }

    public static Expression Count(Expression argument) => argument.Apply(CountOf.Instance);

    /// <param name="default">A literal of the argument's type.</param>
    public static Expression Fallback(Expression argument, Expression @default) =>
        argument.Apply(new FallbackOf(@default));

    /// <param name="arguments">Two or more expressions of one type.</param>
    public static Expression Preferred(ImmutableArray<Expression> arguments) =>
        arguments[0].Apply(new PreferredOf(arguments));

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

    private sealed class PreferredOf(ImmutableArray<Expression> arguments) : IExpressionFunc<Expression>
    {
        public Expression Invoke<T>(Expression<T> first) where T : notnull =>
            new Preferred<T>([.. arguments.Select(argument => (Expression<T>)argument)]);
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
