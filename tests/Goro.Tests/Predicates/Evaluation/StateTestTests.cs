using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Bound;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>The state test truth table of the predicate documentation, in full.</summary>
public class StateTestTests
{
    public enum Bag
    {
        Absent,
        OneUsable,
        OneUnusable,
        OneOfEach,
        TwoUnusable,
    }

    //                    IS ABSENT  IS USABLE  IS UNUSABLE  ALL IS USABLE  ALL IS UNUSABLE
    [TestCase(Bag.Absent, true, false, false, false, false)]
    [TestCase(Bag.OneUsable, false, true, false, true, false)]
    [TestCase(Bag.OneUnusable, false, false, true, false, true)]
    [TestCase(Bag.OneOfEach, false, true, true, false, false)]
    [TestCase(Bag.TwoUnusable, false, false, true, false, true)]
    public void TruthTable(Bag bag, bool absent, bool usable, bool unusable, bool allUsable, bool allUnusable)
    {
        var p = new TestPredicate();
        var x = p.Id("x", bag switch
        {
            Bag.Absent => [],
            Bag.OneUsable => [Ok(1m)],
            Bag.OneUnusable => [BadNumber],
            Bag.OneOfEach => [Ok(1m), BadNumber],
            _ => new Occurrence<decimal>[] { BadNumber, BadNumber },
        });

        Outcome Decide(Goro.Predicates.Binding.StateTest<decimal> test) => p.Decide(test);

        Assert.Multiple(() =>
        {
            Assert.That(Decide(IsState(x, TestedState.Absent)).Truth, Is.EqualTo(Truths.Of(absent)), "IS ABSENT");
            Assert.That(Decide(IsState(x, TestedState.Usable)).Truth, Is.EqualTo(Truths.Of(usable)), "IS USABLE");
            Assert.That(Decide(IsState(x, TestedState.Unusable)).Truth, Is.EqualTo(Truths.Of(unusable)), "IS UNUSABLE");
            Assert.That(Decide(IsState(All(x), TestedState.Usable)).Truth, Is.EqualTo(Truths.Of(allUsable)), "ALL(x) IS USABLE");
            Assert.That(Decide(IsState(All(x), TestedState.Unusable)).Truth, Is.EqualTo(Truths.Of(allUnusable)), "ALL(x) IS UNUSABLE");
            Assert.That(Decide(IsState(Any(x), TestedState.Usable)).Truth, Is.EqualTo(Truths.Of(usable)), "ANY(x) IS USABLE");
        });
    }

    // Where ALL(x) IS USABLE and NOT x IS UNUSABLE part company.
    [Test]
    public void AbsentRow_SeparatesTheConservativeGuardFromTheNegatedOne()
    {
        var p = new TestPredicate();
        var x = p.Id<decimal>("x");

        Assert.That(p.Decide(IsState(All(x), TestedState.Usable)).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Not(IsState(x, TestedState.Unusable))).Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void NeverReports()
    {
        var p = new TestPredicate();
        var x = p.Id("x", Ok(1m), BadNumber);

        foreach (var state in Enum.GetValues<TestedState>())
        {
            Assert.That(p.Decide(IsState(x, state)).Reported, Is.Empty);
            Assert.That(p.Decide(IsState(All(x), state)).Reported, Is.Empty);
        }
    }
}
