using Goro.Predicates.Identifiers.Interpretation;

namespace Goro.Tests.Predicates.Identifiers.Interpretation;

/// <summary>"Parsing dates" in docs/features/builtins/identifiers.md.</summary>
public class DateShapesTests
{
    [TestCase("1991")]
    [TestCase("1991-01")]
    [TestCase("1991-01-02")]
    [TestCase("1991-01-02T12")]
    [TestCase("1991-01-02T12:30")]
    [TestCase("1991-01-02T12:30:59")]
    public void EachAcceptedForm_GivesItsYear(string text)
    {
        Assert.That(DateShapes.Year(text), Is.EqualTo(1991));
    }

    [Test]
    public void ALeapDay_IsARealDate()
    {
        Assert.That(DateShapes.Year("2024-02-29"), Is.EqualTo(2024));
        Assert.That(DateShapes.Year("2000-02-29"), Is.EqualTo(2000));
    }

    [TestCase("0000", TestName = "there is no year zero")]
    [TestCase("0000-01-01", TestName = "no year zero in a full date either")]
    [TestCase("2023-02-29", TestName = "a leap day in a common year")]
    [TestCase("1900-02-29", TestName = "a leap day in a century that is not a leap year")]
    [TestCase("1991-13", TestName = "month 13")]
    [TestCase("1991-00", TestName = "month 0")]
    [TestCase("1991-04-31", TestName = "day 31 of a 30-day month")]
    [TestCase("1991-01-00", TestName = "day 0")]
    [TestCase("1991-01-02T24", TestName = "hour 24")]
    [TestCase("1991-01-02T12:60", TestName = "minute 60")]
    [TestCase("1991-01-02T12:30:60", TestName = "second 60")]
    [TestCase("1991-1", TestName = "a one-digit month")]
    [TestCase("1991/01/02", TestName = "slashes")]
    [TestCase("1991-01-02 12:30", TestName = "a space for the T")]
    [TestCase("1991-01-02t12", TestName = "a lower-case t")]
    [TestCase("91", TestName = "a two-digit year")]
    [TestCase("19910", TestName = "a five-digit year")]
    [TestCase("1991-01-02T12:30:59Z", TestName = "a time zone")]
    [TestCase("1991/1992", TestName = "a range written with a slash")]
    [TestCase("last tuesday", TestName = "words")]
    [TestCase("١٩٩١", TestName = "digits that are not ASCII")]
    [TestCase("+991", TestName = "a sign in the year")]
    [TestCase("1991-+1", TestName = "a sign in the month")]
    [TestCase("", TestName = "nothing")]
    public void AnythingElse_IsUnusable(string text)
    {
        Assert.That(DateShapes.Year(text), Is.Null);
    }
}
