using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>What each comparison operator holds of one pair of data, for every type it compares.</summary>
public class ComparisonTests
{
    private static readonly TestPredicate P = new();

    [TestCase(ComparisonOperator.Equal, 1, 2, Truth.False)]
    [TestCase(ComparisonOperator.Equal, 2, 2, Truth.True)]
    [TestCase(ComparisonOperator.NotEqual, 1, 2, Truth.True)]
    [TestCase(ComparisonOperator.NotEqual, 2, 2, Truth.False)]
    [TestCase(ComparisonOperator.Less, 1, 2, Truth.True)]
    [TestCase(ComparisonOperator.Less, 2, 2, Truth.False)]
    [TestCase(ComparisonOperator.Less, 3, 2, Truth.False)]
    [TestCase(ComparisonOperator.LessOrEqual, 2, 2, Truth.True)]
    [TestCase(ComparisonOperator.LessOrEqual, 3, 2, Truth.False)]
    [TestCase(ComparisonOperator.Greater, 3, 2, Truth.True)]
    [TestCase(ComparisonOperator.Greater, 2, 2, Truth.False)]
    [TestCase(ComparisonOperator.GreaterOrEqual, 2, 2, Truth.True)]
    [TestCase(ComparisonOperator.GreaterOrEqual, 1, 2, Truth.False)]
    public void Numbers(ComparisonOperator @operator, decimal left, decimal right, Truth expected)
    {
        Assert.That(P.Decide(Compare(Lit(left), @operator, Lit(right))).Truth, Is.EqualTo(expected));
    }

    [Test]
    public void ByteCountsAndDurations_CompareByMagnitude()
    {
        Assert.That(P.Decide(Lt(Lit(new ByteCount(1000)), Lit(new ByteCount(1024)))).Truth, Is.EqualTo(Truth.True));
        Assert.That(P.Decide(Eq(Lit(new ByteCount(1433.6m)), Lit(new ByteCount(1433.6m)))).Truth, Is.EqualTo(Truth.True));
        Assert.That(P.Decide(Gt(Lit(new Duration(90)), Lit(new Duration(60)))).Truth, Is.EqualTo(Truth.True));
        Assert.That(P.Decide(Eq(Lit(new Duration(90)), Lit(new Duration(60)))).Truth, Is.EqualTo(Truth.False));
    }

    [TestCase("abc", ComparisonOperator.Equal, "abc", Truth.True)]
    [TestCase("abc", ComparisonOperator.Equal, "ABC", Truth.False)]
    [TestCase("2", ComparisonOperator.Greater, "10", Truth.True)]
    [TestCase("ab", ComparisonOperator.Less, "abc", Truth.True)]
    public void Strings_UseTheOperatorsOrder(string left, ComparisonOperator @operator, string right, Truth expected)
    {
        Assert.That(P.Decide(Compare(Lit(left), @operator, Lit(right))).Truth, Is.EqualTo(expected));
    }

    // (a == b) == (c == d): booleans compare with == and != like any other datum.
    [TestCase(true, ComparisonOperator.Equal, true, Truth.True)]
    [TestCase(true, ComparisonOperator.Equal, false, Truth.False)]
    [TestCase(false, ComparisonOperator.NotEqual, true, Truth.True)]
    [TestCase(false, ComparisonOperator.NotEqual, false, Truth.False)]
    public void Booleans_CompareForEquality(bool left, ComparisonOperator @operator, bool right, Truth expected)
    {
        var a = Eq(Lit(1m), Lit(left ? 1m : 2m));
        var b = Eq(Lit("x"), Lit(right ? "x" : "y"));

        Assert.That(P.Decide(Compare<bool>(a, @operator, b)).Truth, Is.EqualTo(expected));
    }

    [Test]
    public void EveryDatum_IsPreparedBeforeItIsCompared()
    {
        var order = new UpperCasingOrder();
        var p = new TestPredicate();

        var outcome = p.Decide(Compare(p.Id("artist", Ok("Metallica")), ComparisonOperator.Equal, Lit("metallica"), order));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
    }

    // Each datum is prepared once, not once for every combination it takes part in; an unusable
    // occurrence has no datum to prepare.
    [Test]
    public void EachDatum_IsPreparedOnce()
    {
        var order = new UpperCasingOrder();
        var p = new TestPredicate();
        var a = p.Id("a", Ok("a1"), Ok("a2"), BadString);
        var b = p.Id("b", Ok("b1"), Ok("b2"), Ok("b3"));

        p.Decide(Compare(a, ComparisonOperator.Equal, b, order));

        Assert.That(order.Prepared, Is.EquivalentTo(new[] { "a1", "a2", "b1", "b2", "b3" }));
    }

    [Test]
    public void AnAbsentOperand_PreparesNothing()
    {
        var order = new UpperCasingOrder();
        var p = new TestPredicate();

        p.Decide(Compare(p.Id<string>("a"), ComparisonOperator.Equal, p.Id("b", Ok("b1")), order));

        Assert.That(order.Prepared, Is.Empty);
    }
}
