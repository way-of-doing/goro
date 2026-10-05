using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Bound;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>Where absent and unusable part company: negation, and the readings of <c>!=</c>.</summary>
public class NegationTests
{
    public enum Bag
    {
        Absent,
        Unusable,
        OneOfEach,
    }

    //                    ANY(x) != v     ALL(x) != v     NOT x == v
    [TestCase(Bag.Absent, Truth.False, Truth.False, Truth.True)]
    [TestCase(Bag.Unusable, Truth.Unusable, Truth.Unusable, Truth.Unusable)]
    [TestCase(Bag.OneOfEach, Truth.Unusable, Truth.False, Truth.False)]
    public void TheReadingsOfNotEqual(Bag bag, Truth anyNotEqual, Truth allNotEqual, Truth notEqual)
    {
        var p = new TestPredicate();
        var x = p.Id("x", bag switch
        {
            Bag.Absent => [],
            Bag.Unusable => [BadNumber],
            _ => new Occurrence<decimal>[] { Ok(1m), BadNumber },
        });
        var v = Lit(1m);

        Assert.Multiple(() =>
        {
            Assert.That(p.Decide(Ne(Any(x), v)).Truth, Is.EqualTo(anyNotEqual), "ANY(x) != v");
            Assert.That(p.Decide(Ne(All(x), v)).Truth, Is.EqualTo(allNotEqual), "ALL(x) != v");
            Assert.That(p.Decide(Not(Eq(x, v))).Truth, Is.EqualTo(notEqual), "NOT x == v");
        });
    }

    // Negation must not turn a comparison that could not be answered into a true one.
    [Test]
    public void ComparisonAndNegatedComplement_AreBothUnusable_ForAnUnusableValue()
    {
        var p = new TestPredicate();
        var x = p.Id("x", BadNumber);

        Assert.That(p.Decide(Lt(x, Lit(2000m))).Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(p.Decide(Not(Ge(x, Lit(2000m)))).Truth, Is.EqualTo(Truth.Unusable));
    }

    [Test]
    public void ComparisonAndNegatedComplement_Differ_ForAnAbsentValue()
    {
        var p = new TestPredicate();
        var x = p.Id<decimal>("x");

        Assert.That(p.Decide(Lt(x, Lit(2000m))).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Not(Ge(x, Lit(2000m)))).Truth, Is.EqualTo(Truth.True));
    }
}
