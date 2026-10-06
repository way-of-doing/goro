using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>The logical operators' table from the predicate documentation, in both operand orders.</summary>
public class LogicalTests
{
    private static readonly TestPredicate P = new();

    //        a                b                a AND b          a OR b
    [TestCase(Truth.True, Truth.True, Truth.True, Truth.True)]
    [TestCase(Truth.True, Truth.False, Truth.False, Truth.True)]
    [TestCase(Truth.False, Truth.False, Truth.False, Truth.False)]
    [TestCase(Truth.Unusable, Truth.True, Truth.Unusable, Truth.True)]
    [TestCase(Truth.Unusable, Truth.False, Truth.False, Truth.Unusable)]
    [TestCase(Truth.Unusable, Truth.Unusable, Truth.Unusable, Truth.Unusable)]
    public void TruthTable_InBothOperandOrders(Truth a, Truth b, Truth and, Truth or)
    {
        Assert.Multiple(() =>
        {
            Assert.That(P.Decide(And(new Probe(a), new Probe(b))).Truth, Is.EqualTo(and), "a AND b");
            Assert.That(P.Decide(And(new Probe(b), new Probe(a))).Truth, Is.EqualTo(and), "b AND a");
            Assert.That(P.Decide(Or(new Probe(a), new Probe(b))).Truth, Is.EqualTo(or), "a OR b");
            Assert.That(P.Decide(Or(new Probe(b), new Probe(a))).Truth, Is.EqualTo(or), "b OR a");
        });
    }

    [TestCase(Truth.True, Truth.False)]
    [TestCase(Truth.False, Truth.True)]
    [TestCase(Truth.Unusable, Truth.Unusable)]
    public void Not_TruthTable(Truth a, Truth expected)
    {
        Assert.That(P.Decide(Not(new Probe(a))).Truth, Is.EqualTo(expected));
    }

    [Test]
    public void NotNot_IsTheOperand()
    {
        foreach (var truth in Enum.GetValues<Truth>())
        {
            Assert.That(P.Decide(Not(Not(new Probe(truth)))).Truth, Is.EqualTo(truth));
        }
    }

    // Only a false first operand settles AND, and only a true one OR; an unusable one settles neither.
    [TestCase(Truth.False, false)]
    [TestCase(Truth.True, true)]
    [TestCase(Truth.Unusable, true)]
    public void And_EvaluatesItsSecondOperand_UnlessTheFirstIsFalse(Truth first, bool secondEvaluated)
    {
        var second = new Probe(Truth.True);

        P.Decide(And(new Probe(first), second));

        Assert.That(second.Decisions, Is.EqualTo(secondEvaluated ? 1 : 0));
    }

    [TestCase(Truth.True, false)]
    [TestCase(Truth.False, true)]
    [TestCase(Truth.Unusable, true)]
    public void Or_EvaluatesItsSecondOperand_UnlessTheFirstIsTrue(Truth first, bool secondEvaluated)
    {
        var second = new Probe(Truth.True);

        P.Decide(Or(new Probe(first), second));

        Assert.That(second.Decisions, Is.EqualTo(secondEvaluated ? 1 : 0));
    }

    [Test]
    public void LogicalOperators_NeverReport()
    {
        var outcome = P.Decide(Or(And(new Probe(Truth.Unusable), new Probe(Truth.True)), Not(new Probe(Truth.Unusable))));

        Assert.That(outcome.Reported, Is.Empty);
    }

    [Test]
    public void LogicalOperands_MustBeExactlyOne()
    {
        var p = new TestPredicate();
        var flag = p.Id<bool>("compilation");

        Assert.Throws<ArgumentException>(() => Not(flag));
        Assert.Throws<ArgumentException>(() => And(True, flag));
        Assert.Throws<ArgumentException>(() => Or(flag, True));
    }
}
