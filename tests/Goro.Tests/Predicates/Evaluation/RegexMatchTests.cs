using Goro.Predicates.Text;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Evaluation;

public class RegexMatchTests
{
    [Test]
    public void AbsentSubject_IsFalse()
    {
        var p = new TestPredicate();

        Assert.That(p.Decide(Matches(All(p.Id<string>("artist")), "a")).Truth, Is.EqualTo(Truth.False));
    }

    [Test]
    public void UnusableSubject_IsUnusable_AndReported()
    {
        var p = new TestPredicate();
        var artist = p.Id("artist", BadString, BadString);

        var outcome = p.Decide(Matches(artist, "a"));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(outcome.Reported, Is.EqualTo(new[] { artist.Origin }));
    }

    [Test]
    public void MatchesAnySubstring_AndIsQuantified()
    {
        var p = new TestPredicate();
        var genre = p.Id("genre", Ok("rock"), Ok("jazz"));

        Assert.That(p.Decide(Matches(genre, "ock")).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Matches(All(genre), "ock")).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Matches(All(genre), "^[a-z]+$")).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Matches(genre, "^ock")).Truth, Is.EqualTo(Truth.False));
    }

    [Test]
    public void AMatchAmongUsableOccurrences_StillReportsTheUnusableOne()
    {
        var p = new TestPredicate();
        var genre = p.Id("genre", Ok("rock"), BadString);

        var outcome = p.Decide(Matches(genre, "rock"));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Reported, Is.EqualTo(new[] { genre.Origin }));
    }

    [Test]
    public void CaseDifference_MatchesByDefault_ButNotLiterally()
    {
        var p = new TestPredicate();
        var artist = p.Id("artist", Ok("Metallica"));

        Assert.That(p.Decide(Matches(artist, "^met")).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Matches(artist, "^met", ComparisonMode.Literal)).Truth, Is.EqualTo(Truth.False));
    }

    // The subject is normalized and the pattern is not, so a diacritic in the pattern has nothing
    // to match; under LITERALLY() the subject keeps its diacritics.
    [Test]
    public void DiacriticInPattern_MatchesNothing_ButMatchesLiterally()
    {
        var p = new TestPredicate();
        var artist = p.Id("artist", Ok("Motörhead"));

        Assert.That(p.Decide(Matches(artist, "motö")).Truth, Is.EqualTo(Truth.False));
        Assert.That(p.Decide(Matches(artist, "mot")).Truth, Is.EqualTo(Truth.True));
        Assert.That(p.Decide(Matches(artist, "otö", ComparisonMode.Literal)).Truth, Is.EqualTo(Truth.True));
    }

    // e, then an optional combining acute: composed, it would become an optional é instead.
    [Test]
    public void PatternIsMatchedAsWritten()
    {
        var p = new TestPredicate();

        Assert.That(p.Decide(Matches(p.Id("title", Ok("e")), "^é?$")).Truth, Is.EqualTo(Truth.True));
    }
}
