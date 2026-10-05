using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Evaluation;

public class EvaluationContextTests
{
    private static readonly FileData File = new("any");

    [Test]
    public void File_IsTheOneGiven()
    {
        Assert.That(new EvaluationContext(File, 0).File, Is.SameAs(File));
    }

    [Test]
    public void Reported_BeforeAnyReport_IsEmpty()
    {
        Assert.That(new EvaluationContext(File, 3).Reported, Is.Empty);
    }

    [Test]
    public void Report_SameSourceTwice_KeepsTheFirstOrigin()
    {
        var context = new EvaluationContext(File, 2);
        var first = new Origin(new SourceId(1), "x");
        context.Report(first);
        context.Report(new Origin(new SourceId(1), "X"));

        Assert.That(context.Reported, Is.EqualTo(new[] { first }));
    }

    // A file's warnings come out in an order that depends on the predicate alone, not on the
    // order in which an operator happened to reach the occurrences of a bag.
    [Test]
    public void Reported_IsInOrderOfSource_NotOfReport()
    {
        var context = new EvaluationContext(File, 70);
        var late = new Origin(new SourceId(65), "late");
        var middle = new Origin(new SourceId(2), "middle");
        var early = new Origin(new SourceId(0), "early");
        context.Report(late);
        context.Report(middle);
        context.Report(early);

        Assert.That(context.Reported, Is.EqualTo(new[] { early, middle, late }));
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void Report_SourceThePredicateCannotReport_Throws(int source)
    {
        var context = new EvaluationContext(File, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => context.Report(new Origin(new SourceId(source), "x")));
    }

    [Test]
    public void Report_WithNoSources_Throws()
    {
        var context = new EvaluationContext(File, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => context.Report(new Origin(new SourceId(0), "x")));
    }
}
