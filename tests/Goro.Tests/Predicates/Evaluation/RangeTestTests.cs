using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Bound;

namespace Goro.Tests.Predicates.Evaluation;

public class RangeTestTests
{
    [TestCase(0, Truth.False)]
    [TestCase(1, Truth.True)]
    [TestCase(5, Truth.True)]
    [TestCase(10, Truth.True)]
    [TestCase(11, Truth.False)]
    public void EndpointsAreInclusive(decimal datum, Truth expected)
    {
        var p = new TestPredicate();

        Assert.That(p.Decide(Between(p.Id("v", Ok(datum)), 1m, 10m)).Truth, Is.EqualTo(expected));
    }

    // BETWEEN is one operator: both bounds are tested of the same occurrence. The pair of
    // comparisons it would be desugared into finds a different occurrence for each bound.
    [Test]
    public void IsNotAPairOfComparisons()
    {
        var p = new TestPredicate();
        var v = p.Id("v", Ok(0m), Ok(20m));

        Assert.That(p.Decide(Between(v, 1m, 10m)).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(And(Ge(v, Lit(1m)), Le(v, Lit(10m)))).Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void SubjectIsQuantified()
    {
        var p = new TestPredicate();
        var v = p.Id("v", Ok(2m), Ok(20m));

        Assert.That(p.Decide(Between(v, 1m, 10m)).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Between(All(v), 1m, 10m)).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Between(All(p.Id("w", Ok(2m), Ok(3m))), 1m, 10m)).Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void AbsentSubject_IsFalse_EvenUnderAll()
    {
        var p = new TestPredicate();

        Assert.That(p.Decide(Between(All(p.Id<decimal>("v")), 1m, 10m)).Truth, Is.EqualTo(Truth.False));
    }

    [Test]
    public void UnusableSubject_IsUnusable_AndReported()
    {
        var p = new TestPredicate();
        var v = p.Id("v", BadNumber);

        var outcome = p.Decide(Between(v, 1m, 10m));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(outcome.Reported, Is.EqualTo(new[] { v.Origin }));
    }

    // The binder hands the endpoints over prepared; only the subject's data are prepared here.
    [Test]
    public void OnlyTheSubjectIsPrepared()
    {
        var order = new UpperCasingOrder();
        var p = new TestPredicate();

        var upper = p.Decide(Between(p.Id("s", Ok("b")), "A", "C", order));
        var lower = p.Decide(Between(p.Id("t", Ok("b")), "a", "c", order));

        Assert.That(upper.Truth, Is.EqualTo(Truth.True));
        Assert.That(lower.Truth, Is.EqualTo(Truth.False), "\"B\" sorts before \"a\" ordinally");
        Assert.That(order.Prepared, Is.EqualTo(new[] { "b", "b" }));
    }
}
