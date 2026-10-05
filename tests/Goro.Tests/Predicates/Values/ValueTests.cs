using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Values;

public class ValueTests
{
    [Test]
    public void Absent_HasNoOccurrences()
    {
        Assert.That(Value<string>.Absent.IsAbsent, Is.True);
        Assert.That(Value<string>.Absent.Occurrences, Is.Empty);
    }

    [Test]
    public void Default_IsAbsent()
    {
        Assert.That(default(Value<decimal>).IsAbsent, Is.True);
    }

    // There is no empty bag distinct from absence.
    [Test]
    public void Of_NoOccurrences_IsAbsent()
    {
        Assert.That(Value<string>.Of().IsAbsent, Is.True);
        Assert.That(Value<string>.Of(Enumerable.Empty<Occurrence<string>>()).IsAbsent, Is.True);
    }

    [Test]
    public void Of_KeepsDuplicates()
    {
        var value = Value<string>.Of(new Usable<string>("rock"), new Usable<string>("rock"));

        Assert.That(value.IsAbsent, Is.False);
        Assert.That(value.Occurrences, Has.Length.EqualTo(2));
    }

    [Test]
    public void Single_HoldsOneOccurrence()
    {
        var unusable = new Unusable<decimal>(new Origin(new SourceId(0), "year"));

        Assert.That(Value<decimal>.Single(unusable).Occurrences, Is.EqualTo(new[] { unusable }));
    }
}
