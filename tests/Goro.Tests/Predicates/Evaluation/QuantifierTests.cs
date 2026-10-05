using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Bound;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>Iteration over multivalue operands: nesting by quantifier, and its consequences.</summary>
public class QuantifierTests
{
    // a = {1, 2} and b = {2, 3}, or b = {1, 2, 3} where containment is the point.
    [Test]
    public void BothExistential_TrueIfSomePairHolds_InEitherOrder()
    {
        var p = new TestPredicate();
        var a = p.Id("a", Ok(1m), Ok(2m));
        var b = p.Id("b", Ok(2m), Ok(3m));

        Assert.That(p.Decide(Eq(a, b)).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Eq(b, a)).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Ne(Any(a), Any(b))).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Ne(Any(b), Any(a))).Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void BothUniversal_TrueOnlyIfEveryPairHolds_InEitherOrder()
    {
        var p = new TestPredicate();
        var a = p.Id("a", Ok(1m), Ok(2m));
        var b = p.Id("b", Ok(2m), Ok(3m));
        var twos = p.Id("twos", Ok(2m), Ok(2m));
        var alsoTwos = p.Id("alsoTwos", Ok(2m));

        Assert.That(p.Decide(Eq(All(a), All(b))).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Eq(All(b), All(a))).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Eq(All(twos), All(alsoTwos))).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Eq(All(alsoTwos), All(twos))).Truth, Is.EqualTo(Truth.True));
    }

    // Exactly one universal operand forms the outer loop whichever side it is written on.
    [Test]
    public void OneUniversal_IsTheOuterLoop_WhicheverSideItIsOn()
    {
        var p = new TestPredicate();
        var a = p.Id("a", Ok(1m), Ok(2m));
        var b = p.Id("b", Ok(1m), Ok(2m), Ok(3m));

        Assert.That(p.Decide(Eq(All(a), b)).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Eq(b, All(a))).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Eq(All(b), a)).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Eq(a, All(b))).Truth, Is.EqualTo(Truth.False));
    }

    // ALL(a) < b compares aggregates: the largest of a is below the largest of b. It does not say
    // every value of a is below every value of b, which is ALL(a) < ALL(b).
    [Test]
    public void OrderingWithOneUniversal_ComparesAggregates()
    {
        var p = new TestPredicate();
        var a = p.Id("a", Ok(1m), Ok(2m));
        var b = p.Id("b", Ok(2m), Ok(3m));

        Assert.That(p.Decide(Lt(All(a), b)).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Gt(b, All(a))).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Lt(All(a), All(b))).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Gt(All(b), All(a))).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Lt(a, All(b))).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Gt(All(b), a)).Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void Universal_IsNotVacuous()
    {
        var p = new TestPredicate();
        var x = p.Id<decimal>("x");

        Assert.That(p.Decide(Eq(All(x), Lit(1m))).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Ne(All(x), Lit(1m))).Truth, Is.EqualTo(Truth.False));
    }

    [Test]
    public void AnAbsentOperand_IsFalse_WhateverTheOtherHolds()
    {
        var p = new TestPredicate();
        var absent = p.Id<decimal>("absent");
        var bad = p.Id("bad", BadNumber);

        var outcome = p.Decide(Eq(absent, bad));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.False));
        Assert.That(outcome.Reported, Is.Empty, "Absence is decided before iteration, so nothing is consumed.");
    }

    // x == v, ALL(x) == v and ALL(x) == w for x holding v and one unusable occurrence: an answered
    // combination settles a quantifier whenever it can, and only otherwise does the unusable one decide.
    [Test]
    public void AnAnsweredCombination_SettlesTheQuantifierWheneverItCan()
    {
        var p = new TestPredicate();
        var x = p.Id("x", Ok(1991m), BadNumber);

        Assert.That(p.Decide(Eq(x, Lit(1991m))).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Eq(All(x), Lit(1991m))).Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(p.Decide(Eq(All(x), Lit(2000m))).Truth, Is.EqualTo(Truth.False));
    }

    // The property nesting by quantifier exists to deliver, asserted over every operator, every
    // pair of quantifiers and every pair of a set of bags that includes absence and unusables.
    [Test]
    public void OperandOrder_NeverChangesTheResult_OrTheReports()
    {
        Occurrence<decimal>[][] bags =
        [
            [], [Ok(1m)], [Ok(2m)], [BadNumber], [Ok(1m), Ok(2m)], [Ok(2m), Ok(3m)], [Ok(1m), Ok(2m), Ok(3m)],
            [Ok(1m), BadNumber], [Ok(3m), BadNumber], [BadNumber, BadNumber],
        ];
        Func<Goro.Predicates.Binding.BoundExpression<decimal>, Goro.Predicates.Binding.BoundExpression<decimal>>[] quantifiers = [e => e, All];
        var cases = 0;

        Assert.Multiple(() =>
        {
            foreach (var @operator in Enum.GetValues<ComparisonOperator>())
            foreach (var aBag in bags)
            foreach (var bBag in bags)
            foreach (var aQuantifier in quantifiers)
            foreach (var bQuantifier in quantifiers)
            {
                var p = new TestPredicate();
                var a = aQuantifier(p.Id("a", aBag));
                var b = bQuantifier(p.Id("b", bBag));

                var written = p.Decide(Compare(a, @operator, b));
                var mirrored = p.Decide(Compare(b, Mirror(@operator), a));

                var description = $"{@operator} over {Show(aBag)} and {Show(bBag)}";
                Assert.That(mirrored.Truth, Is.EqualTo(written.Truth), description);
                Assert.That(mirrored.Reported, Is.EqualTo(written.Reported), description);
                cases++;
            }
        });
        Assert.That(cases, Is.EqualTo(6 * 10 * 10 * 4));
    }

    private static string Show(Occurrence<decimal>[] bag) =>
        bag.Length == 0 ? "absent" : $"{{{string.Join(", ", bag.Select(o => o is Usable<decimal>(var d) ? $"{d}" : "bad"))}}}";
}
