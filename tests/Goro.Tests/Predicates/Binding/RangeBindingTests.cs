using Goro.Messages;
using Goro.Predicates.Binding;
using Goro.Predicates.Text;
using Goro.Predicates.Values;
using static Goro.Tests.Predicates.Binding.Support.BindAssert;
using Codes = Goro.Predicates.Binding.BinderDiagnosticCodes;

namespace Goro.Tests.Predicates.Binding;

/// <summary>
/// Ranges: two literals of one type as written, in order as the operator will compare them, and
/// handed to the range test already prepared.
/// </summary>
public class RangeBindingTests
{
    // 60..120kb is an error whatever the subject: it is unclear what unit 60 is in.
    [TestCase("file::size BETWEEN 60..120kb", "60", "file::size BETWEEN 60kb..120kb")]
    [TestCase("year BETWEEN 60..120kb", "60", "year BETWEEN 60kb..120kb")]
    [TestCase("file::size BETWEEN 1kib..2", "2", "file::size BETWEEN 1kib..2kib")]
    [TestCase("file::size BETWEEN .5..1.5MB", ".5", "file::size BETWEEN .5MB..1.5MB")]
    [TestCase("file::duration BETWEEN 60..2m", "60", "file::duration BETWEEN 60m..2m")]
    public void Range_OfANumberAndAUnit_AsksForTheUnit(string text, string marked, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.RangeUnitMissing));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void Range_OfANumberAndAUnit_SaysWhatIsUnclear()
    {
        Assert.That(Error("file::size BETWEEN 60..120kb").Message,
            Is.EqualTo(new ErrorMessage.RangeUnitAmbiguous(new Code("60"), UnitType.ByteCount, new Code("60kb"))));
    }

    [TestCase("file::duration BETWEEN 60..1:00")]
    [TestCase("file::duration BETWEEN 1.5..2m")]
    [TestCase("file::duration BETWEEN 60..1h30m")]
    public void Range_OfANumberAndAUnit_ThatCannotBeRewritten_OffersNothing(string text)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.RangeUnitMissing));
        Assert.That(error.Suggestions, Is.Empty);
    }

    [TestCase("year BETWEEN \"a\"..5")]
    [TestCase("file::size BETWEEN 1kb..1m")]
    public void Range_OfMixedTypes_IsAnError(string text)
    {
        Assert.That(Error(text).Code, Is.EqualTo(Codes.RangeEndpointTypes));
    }

    [Test]
    public void Range_OfNumbers_StandsForBytecounts()
    {
        var range = (RangeTest<ByteCount>)Compiles("file::size BETWEEN 60..120").Root;

        Assert.That((range.Minimum, range.Maximum), Is.EqualTo((new ByteCount(60), new ByteCount(120))));
    }

    [TestCase("file::duration BETWEEN 1.5..2", Codes.FractionalDurationLiteral)]
    [TestCase("file::size BETWEEN -1..2", Codes.NegativeUnitLiteral)]
    [TestCase("artist BETWEEN 1..2", Codes.TypeMismatch)]
    [TestCase("year BETWEEN 1kb..2kb", Codes.TypeMismatch)]
    public void Range_ThatDoesNotFitItsSubject_IsAnError(string text, string code)
    {
        Assert.That(Error(text).Code, Is.EqualTo(code));
    }

    [TestCase("year BETWEEN 2000..1990", "year BETWEEN 1990..2000")]
    [TestCase("file::size BETWEEN 2mb..1mb", "file::size BETWEEN 1mb..2mb")]
    [TestCase("file::size BETWEEN 2000..1kb", null)]
    [TestCase("artist BETWEEN \"n\"..\"M\"", "artist BETWEEN \"M\"..\"n\"")]
    public void Range_WithMinAboveMax_IsAnError(string text, string? rewrite)
    {
        var error = Error(text);

        if (rewrite is null)
        {
            Assert.That(error.Code, Is.EqualTo(Codes.RangeUnitMissing));
            return;
        }

        Assert.That(error.Code, Is.EqualTo(Codes.RangeReversed));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    // The ends are compared the way the operator will compare them.
    [Test]
    public void Range_OfStrings_IsOrderedInTheOperatorsMode()
    {
        Compiles("LITERALLY(artist) BETWEEN \"B\"..\"a\"");
        Assert.That(Error("artist BETWEEN \"B\"..\"a\"").Code, Is.EqualTo(Codes.RangeReversed));
        Assert.That(Error("LITERALLY(artist) BETWEEN \"a\"..\"B\"").Code, Is.EqualTo(Codes.RangeReversed));
    }

    [Test]
    public void Range_WithEqualEnds_IsValid()
    {
        Compiles("year BETWEEN 2000..2000");
        Compiles("artist BETWEEN \"A\"..\"a\"");
    }

    [TestCase("artist BETWEEN \"MA\"..\"Mi\"", "ma", "mi", ComparisonMode.Normalized)]
    [TestCase("LITERALLY(artist) BETWEEN \"MA\"..\"Mi\"", "MA", "Mi", ComparisonMode.Literal)]
    [TestCase("artist BETWEEN \"Mö\"..\"Mü\"", "mo", "mu", ComparisonMode.Normalized)]
    public void Range_EndsArrivePrepared(string text, string minimum, string maximum, ComparisonMode mode)
    {
        var range = (RangeTest<string>)Compiles(text).Root;

        Assert.That((range.Minimum, range.Maximum), Is.EqualTo((minimum, maximum)));
        Assert.That(range.Order, Is.SameAs(StringOrder.For(mode)));
    }

    [Test]
    public void Range_EndsAreCheckedWhateverTheSubject()
    {
        Assert.That(Codes("artst BETWEEN 2..1"), Is.EqualTo(new[] { Codes.UnknownIdentifier, Codes.RangeReversed }));
    }
}
