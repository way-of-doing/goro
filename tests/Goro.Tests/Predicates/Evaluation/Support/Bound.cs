using System.Text.RegularExpressions;
using Goro.Predicates.Binding;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Evaluation.Support;

/// <summary>
/// Builds bound trees that read roughly as the predicate they stand for, so that
/// <c>ALL(m) == "a" OR m == "b"</c> is <c>Or(Eq(All(m), Lit("a")), Eq(m, Lit("b")))</c>. Anything
/// that has a warning source of its own -- identifiers and conversions -- is made by a
/// <see cref="TestPredicate"/>, which interns the sources.
/// </summary>
internal static class Bound
{
    public static Literal<T> Lit<T>(T value) where T : notnull => new(value);

    public static Literal<bool> True => new(true);

    public static Literal<bool> False => new(false);

    public static Usable<T> Ok<T>(T datum) where T : notnull => new(datum);

    /// <summary>An unusable occurrence of a canned bag; the binding gives it the reference's origin.</summary>
    public static Unusable<decimal> BadNumber => new(null);

    public static Unusable<string> BadString => new(null);

    public static BoundExpression<T> All<T>(BoundExpression<T> operand) where T : notnull =>
        new Quantified<T>(operand, Quantifier.Universal);

    public static BoundExpression<T> Any<T>(BoundExpression<T> operand) where T : notnull =>
        new Quantified<T>(operand, Quantifier.Existential);

    public static ComparisonTest<T> Compare<T>(BoundExpression<T> left, ComparisonOperator @operator, BoundExpression<T> right, DatumOrder<T>? order = null)
        where T : notnull => new(Operand(left), @operator, Operand(right), order ?? DefaultOrder<T>());

    public static ComparisonTest<T> Eq<T>(BoundExpression<T> left, BoundExpression<T> right) where T : notnull =>
        Compare(left, ComparisonOperator.Equal, right);

    public static ComparisonTest<T> Ne<T>(BoundExpression<T> left, BoundExpression<T> right) where T : notnull =>
        Compare(left, ComparisonOperator.NotEqual, right);

    public static ComparisonTest<T> Lt<T>(BoundExpression<T> left, BoundExpression<T> right) where T : notnull =>
        Compare(left, ComparisonOperator.Less, right);

    public static ComparisonTest<T> Le<T>(BoundExpression<T> left, BoundExpression<T> right) where T : notnull =>
        Compare(left, ComparisonOperator.LessOrEqual, right);

    public static ComparisonTest<T> Gt<T>(BoundExpression<T> left, BoundExpression<T> right) where T : notnull =>
        Compare(left, ComparisonOperator.Greater, right);

    public static ComparisonTest<T> Ge<T>(BoundExpression<T> left, BoundExpression<T> right) where T : notnull =>
        Compare(left, ComparisonOperator.GreaterOrEqual, right);

    public static RangeTest<T> Between<T>(BoundExpression<T> subject, T minimum, T maximum, DatumOrder<T>? order = null)
        where T : notnull => new(Operand(subject), minimum, maximum, order ?? DefaultOrder<T>());

    /// <summary><c>subject =~ pattern</c>, compiled as the binder compiles a pattern.</summary>
    public static RegexMatch Matches(BoundExpression<string> subject, string pattern, ComparisonMode mode = ComparisonMode.Normalized)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
        if (mode == ComparisonMode.Normalized)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new(Operand(subject), new Regex(pattern, options), mode);
    }

    public static StateTest<T> IsState<T>(BoundExpression<T> operand, TestedState state) where T : notnull
    {
        var bound = Operand(operand);
        return new(bound.Expression, bound.Quantifier, state);
    }

    public static Not Not(BoundExpression<bool> operand) => new(operand);

    public static And And(BoundExpression<bool> left, BoundExpression<bool> right) => new(left, right);

    public static Or Or(BoundExpression<bool> left, BoundExpression<bool> right) => new(left, right);

    public static Count<T> Count<T>(BoundExpression<T> argument) where T : notnull => new(argument);

    public static Fallback<T> Fallback<T>(BoundExpression<T> argument, T @default) where T : notnull => new(argument, @default);

    /// <summary>The mirror image of a comparison operator: <c>a op b</c> says what <c>b mirror a</c> says.</summary>
    public static ComparisonOperator Mirror(ComparisonOperator @operator) => @operator switch
    {
        ComparisonOperator.Less => ComparisonOperator.Greater,
        ComparisonOperator.LessOrEqual => ComparisonOperator.GreaterOrEqual,
        ComparisonOperator.Greater => ComparisonOperator.Less,
        ComparisonOperator.GreaterOrEqual => ComparisonOperator.LessOrEqual,
        _ => @operator,
    };

    private static BoundOperand<T> Operand<T>(BoundExpression<T> expression) where T : notnull =>
        expression is Quantified<T> quantified
            ? new(quantified.Operand, quantified.Quantifier)
            : new(expression, Quantifier.Existential);

    private static DatumOrder<T> DefaultOrder<T>() where T : notnull => (DatumOrder<T>)(typeof(T) switch
    {
        var t when t == typeof(string) => (object)OrdinalOrder.Instance,
        var t when t == typeof(decimal) => NaturalOrder<decimal>.Instance,
        var t when t == typeof(ByteCount) => NaturalOrder<ByteCount>.Instance,
        var t when t == typeof(Duration) => NaturalOrder<Duration>.Instance,
        var t when t == typeof(bool) => NaturalOrder<bool>.Instance,
        var t => throw new NotSupportedException(t.ToString()),
    });

    /// <summary>
    /// A quantifier modifier, as written, until the operator it belongs to takes it off. Like the
    /// modifiers of the syntax tree it never reaches a bound tree; evaluating it is a mistake.
    /// </summary>
    private sealed class Quantified<T>(BoundExpression<T> operand, Quantifier quantifier) : BoundExpression<T>
        where T : notnull
    {
        public BoundExpression<T> Operand { get; } = operand;

        public Quantifier Quantifier { get; } = quantifier;

        public override bool IsDefinite => Operand.IsDefinite;

        public override Value<T> Evaluate(EvaluationContext context) =>
            throw new InvalidOperationException("A modifier belongs to the operator that reads it.");
    }
}
