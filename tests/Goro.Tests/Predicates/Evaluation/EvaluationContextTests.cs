using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Predicates.Evaluation;

public class EvaluationContextTests
{
    private static readonly FileData File = TestFiles.Data("any");

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

    // Which spelling of a source a warning quotes depends on the predicate alone, not on which one
    // an operator happened to reach first.
    [Test]
    public void Report_SameSourceTwice_KeepsTheOriginWrittenEarliest()
    {
        var earlier = new Origin(new SourceId(1), "x", 0);
        var later = new Origin(new SourceId(1), "X", 4);

        var laterFirst = new EvaluationContext(File, 2);
        laterFirst.Report(later);
        laterFirst.Report(earlier);
        var earlierFirst = new EvaluationContext(File, 2);
        earlierFirst.Report(earlier);
        earlierFirst.Report(later);

        Assert.That(laterFirst.Reported, Is.EqualTo(new[] { earlier }));
        Assert.That(earlierFirst.Reported, Is.EqualTo(new[] { earlier }));
    }

    // A file's warnings come out in an order that depends on the predicate alone, not on the
    // order in which an operator happened to reach the occurrences of a bag.
    [Test]
    public void Reported_IsInOrderOfSource_NotOfReport()
    {
        var context = new EvaluationContext(File, 70);
        var late = new Origin(new SourceId(65), "late", 0);
        var middle = new Origin(new SourceId(2), "middle", 0);
        var early = new Origin(new SourceId(0), "early", 0);
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

        Assert.Throws<ArgumentOutOfRangeException>(() => context.Report(new Origin(new SourceId(source), "x", 0)));
    }

    [Test]
    public void Report_WithNoSources_Throws()
    {
        var context = new EvaluationContext(File, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => context.Report(new Origin(new SourceId(0), "x", 0)));
    }
}
