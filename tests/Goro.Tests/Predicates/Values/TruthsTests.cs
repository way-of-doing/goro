using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Values;

public class TruthsTests
{
    [TestCase(Truth.False)]
    [TestCase(Truth.True)]
    [TestCase(Truth.Unusable)]
    public void ToValue_IsOneOccurrenceThatReadsBack(Truth truth)
    {
        var value = truth.ToValue();

        Assert.That(value.Occurrences, Has.Length.EqualTo(1));
        Assert.That(value.ToTruth(), Is.EqualTo(truth));
    }

    [Test]
    public void ToValue_OfUnusable_HasNoOrigin()
    {
        Assert.That(Truth.Unusable.ToValue().Occurrences[0], Is.EqualTo(new Unusable<bool>(null)));
    }

    [Test]
    public void ToTruth_OfUnusableOccurrenceWithOrigin_IsUnusable()
    {
        var value = Value<bool>.Single(new Unusable<bool>(new Origin(new SourceId(3), "compilation", 0)));

        Assert.That(value.ToTruth(), Is.EqualTo(Truth.Unusable));
    }

    [Test]
    public void ToTruth_OfValueThatIsNotExactlyOne_Throws()
    {
        var multivalue = Value<bool>.Of(new Usable<bool>(true), new Usable<bool>(false));

        Assert.Throws<InvalidOperationException>(() => Value<bool>.Absent.ToTruth());
        Assert.Throws<InvalidOperationException>(() => multivalue.ToTruth());
    }
}
