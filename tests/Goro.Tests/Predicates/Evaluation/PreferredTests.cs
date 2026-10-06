using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>
/// <c>PREFERRED()</c>: the first argument holding a usable occurrence, taken entire; failing that,
/// the first that is not absent; failing that, absent. It stops at the argument it chooses and warns
/// about nothing, and neither of those shows in the value it returns, so both are asserted through
/// what evaluating a later argument would have done: resolving it, reporting it, or finding the file
/// unreadable.
/// </summary>
public class PreferredTests
{
    public enum Bag
    {
        Absent,
        Usable,
        Unusable,
        UsableAndUnusable,
    }

    // Each identifier's usable occurrence is a different year, so that the result shows which was chosen.
    private static Occurrence<decimal>[] Holding(Bag bag, decimal year) => bag switch
    {
        Bag.Absent => [],
        Bag.Usable => [Ok(year)],
        Bag.Unusable => [BadNumber],
        Bag.UsableAndUnusable => [Ok(year), BadNumber],
        _ => throw new ArgumentOutOfRangeException(nameof(bag)),
    };

    /// <summary>The bag an identifier resolves to, its unusable occurrences carrying its origin.</summary>
    private static Occurrence<decimal>[] Resolved(IdentifierReference<decimal> identifier, Bag bag, decimal year) =>
        [.. Holding(bag, year).Select(o => o is Unusable<decimal> ? new Unusable<decimal>(identifier.Origin) : o)];

    // The worked table of the predicate documentation, row by row, then the rest of the combinations.
    [TestCase(Bag.Absent, Bag.Usable, "ape")]
    [TestCase(Bag.Unusable, Bag.Usable, "ape")]
    [TestCase(Bag.UsableAndUnusable, Bag.Usable, "vorbis")]
    [TestCase(Bag.Unusable, Bag.Absent, "vorbis")]
    [TestCase(Bag.Absent, Bag.Absent, null)]
    [TestCase(Bag.Usable, Bag.Usable, "vorbis")]
    [TestCase(Bag.Usable, Bag.Absent, "vorbis")]
    [TestCase(Bag.Usable, Bag.Unusable, "vorbis")]
    [TestCase(Bag.Absent, Bag.Unusable, "ape")]
    [TestCase(Bag.Unusable, Bag.Unusable, "vorbis")]
    [TestCase(Bag.Unusable, Bag.UsableAndUnusable, "ape")]
    [TestCase(Bag.Absent, Bag.UsableAndUnusable, "ape")]
    public void TwoArguments_ChooseAsTheTableSays_TakingTheChosenEntire(Bag vorbisHolds, Bag apeHolds, string? chosen)
    {
        var p = new TestPredicate();
        var vorbis = p.Id("vorbis::year", Holding(vorbisHolds, 1991m));
        var ape = p.Id("ape::year", Holding(apeHolds, 2000m));

        var (value, reported) = p.Evaluate(Preferred(vorbis, ape));

        var expected = chosen switch
        {
            "vorbis" => Resolved(vorbis, vorbisHolds, 1991m),
            "ape" => Resolved(ape, apeHolds, 2000m),
            _ => [],
        };
        Assert.That(value.Occurrences, Is.EqualTo(expected));
        Assert.That(value.IsAbsent, Is.EqualTo(chosen is null));
        Assert.That(reported, Is.Empty);
    }

    [Test]
    public void ManyArguments_TheFirstUsableWins_AndTheRestAreNeverResolved()
    {
        var p = new TestPredicate();
        var first = new CannedBinding<decimal>([]);
        var second = new CannedBinding<decimal>([BadNumber]);
        var third = new CannedBinding<decimal>([Ok(3m)]);
        var fourth = new CannedBinding<decimal>([Ok(4m)]);

        var (value, _) = p.Evaluate(Preferred(p.Id("a", first), p.Id("b", second), p.Id("c", third), p.Id("d", fourth)));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(3m) }));
        Assert.That(new[] { first.Resolutions, second.Resolutions, third.Resolutions, fourth.Resolutions },
            Is.EqualTo(new[] { 1, 1, 1, 0 }));
    }

    [Test]
    public void NothingUsable_EveryArgumentIsResolved_AndTheFirstPresentIsChosen()
    {
        var p = new TestPredicate();
        var first = new CannedBinding<decimal>([]);
        var second = new CannedBinding<decimal>([BadNumber, BadNumber]);
        var third = new CannedBinding<decimal>([BadNumber]);
        var b = p.Id("b", second);

        var (value, _) = p.Evaluate(Preferred(p.Id("a", first), b, p.Id("c", third)));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { new Unusable<decimal>(b.Origin), new Unusable<decimal>(b.Origin) }));
        Assert.That(new[] { first.Resolutions, second.Resolutions, third.Resolutions }, Is.EqualTo(new[] { 1, 1, 1 }));
    }

    [Test]
    public void AnArgumentAfterTheChosenOne_WhoseDataWouldMakeTheFileUnreadable_LeavesItReadable()
    {
        var p = new TestPredicate();
        var vorbis = p.Id("vorbis::artist", Ok("Metallica"));
        var ape = p.Id("ape::artist", new UnreadableBinding<string>());

        Assert.That(p.Decide(Eq(Preferred(vorbis, ape), Lit("Metallica"))).Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void AnArgumentReachedBecauseNothingBeforeItIsUsable_CanMakeTheFileUnreadable()
    {
        var p = new TestPredicate();
        var vorbis = p.Id("vorbis::artist", BadString);
        var ape = p.Id("ape::artist", new UnreadableBinding<string>());

        Assert.Throws<UnreadableFileException>(() => p.Decide(Eq(Preferred(vorbis, ape), Lit("metallica"))));
    }

    [Test]
    public void AnArgumentPassedOver_IsNotReported_WhenTheChosenOneIsConsumed()
    {
        var p = new TestPredicate();
        var vorbis = p.Id("vorbis::year", BadNumber);
        var ape = p.Id("ape::year", Ok(1991m));

        var outcome = p.Decide(Eq(Preferred(vorbis, ape), Lit(1991m)));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Reported, Is.Empty);
    }

    [Test]
    public void TheChosenArgumentTakenEntire_IsReported_AndNothingElse()
    {
        var p = new TestPredicate();
        var vorbis = p.Id("vorbis::year", Ok(1991m), BadNumber);
        var ape = p.Id("ape::year", Ok(2000m), BadNumber);

        var outcome = p.Decide(Eq(Preferred(vorbis, ape), Lit(1991m)));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "vorbis::year" }));
    }

    // PREFERRED(x, y) > 1 of the deduplication table: x is chosen and consumed, y discarded.
    [Test]
    public void NothingUsable_TheFirstPresentIsConsumed_AndTheRestAreNotReported()
    {
        var p = new TestPredicate();
        var x = p.Id("x", BadNumber);
        var y = p.Id("y", BadNumber);

        var outcome = p.Decide(Gt(Preferred(x, y), Lit(1m)));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void OfConditions_AnUnansweredOneIsPassedOver_HavingWarnedOnItsOwnAccount()
    {
        var p = new TestPredicate();
        var x = p.Id("x", BadNumber);

        var outcome = p.Decide(Preferred(Gt(x, Lit(1m)), True));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void OfConditions_TheFirstAnsweredOneDecides_AndTheRestAreNotDecided()
    {
        var p = new TestPredicate();
        var later = new Probe(Truth.True);

        var outcome = p.Decide(Preferred(False, later));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.False));
        Assert.That(later.Decisions, Is.Zero);
    }

    [Test]
    public void IsExactlyOne_WhenSomeArgumentIsNeverAbsent_AndNoneIsEverSeveral()
    {
        var p = new TestPredicate();

        Assert.That(Preferred(Lit(1m), Lit(2m)).Bounds, Is.EqualTo(Bounds.ExactlyOne));
        Assert.That(Preferred(Lit(1m), p.Id<decimal>("x")).Bounds, Is.Not.EqualTo(Bounds.ExactlyOne));
        Assert.That(Preferred(p.Id<decimal>("x"), Lit(1m)).Bounds, Is.Not.EqualTo(Bounds.ExactlyOne));
    }
}
