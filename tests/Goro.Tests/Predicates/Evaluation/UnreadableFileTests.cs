using Goro.Predicates.Identifiers;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>A file found unreadable part way through: the evaluation is abandoned, never caught.</summary>
public class UnreadableFileTests
{
    // Every operand is evaluated before absence is checked, as evaluation.md writes it, so an absent
    // left operand does not spare the right one from being read.
    [Test]
    public void AnAbsentOperand_DoesNotSpareTheOtherFromBeingRead()
    {
        var p = new TestPredicate();
        var absent = p.Id<string>("absent");
        var artist = p.Id("artist", new UnreadableBinding<string>());

        Assert.Throws<UnreadableFileException>(() => p.Decide(Eq(absent, artist)));
    }

    [Test]
    public void UnreadableData_AfterAReport_StillAbandonsTheEvaluation()
    {
        var p = new TestPredicate();
        var name = p.Number(p.Id("name", Ok("track one")));
        var artist = p.Id("artist", new UnreadableBinding<string>());

        Assert.Throws<UnreadableFileException>(() => p.Decide(Or(Gt(name, Lit(1m)), Eq(artist, Lit("x")))));
    }

    [Test]
    public void ShortCircuiting_SparesWhatIsNotNeeded()
    {
        var p = new TestPredicate();
        var artist = p.Id("artist", new UnreadableBinding<string>());

        Assert.DoesNotThrow(() => p.Decide(And(False, Eq(artist, Lit("x")))));
    }
}
