using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Bound;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>The expressions that are not operators: literals, identifiers and functions.</summary>
public class FunctionTests
{
    [Test]
    public void Literal_IsOneUsableOccurrence()
    {
        var (value, _) = new TestPredicate().Evaluate(Lit(5m));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(5m) }));
    }

    [Test]
    public void Identifier_ResolvesForTheContextsFile_WithItsOwnOrigin()
    {
        var p = new TestPredicate();
        var binding = new RecordingBinding();
        var x = p.Id("x", binding);

        var (value, reported) = p.Evaluate(x);

        Assert.That(binding.Files, Is.EqualTo(new[] { p.File }));
        Assert.That(value.Occurrences, Is.EqualTo(new[] { new Unusable<decimal>(x.Origin) }));
        Assert.That(reported, Is.Empty);
    }

    [Test]
    public void Count_OfAbsent_IsZero_AndReportsNothing()
    {
        var p = new TestPredicate();

        var (value, reported) = p.Evaluate(Count(p.Id<decimal>("x")));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(0m) }));
        Assert.That(reported, Is.Empty);
    }

    [Test]
    public void Count_OfOneUnusable_IsOne_AndReportsNothing()
    {
        var p = new TestPredicate();

        var (value, reported) = p.Evaluate(Count(p.Id("x", BadNumber)));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(1m) }));
        Assert.That(reported, Is.Empty);
    }

    [Test]
    public void Count_CountsEveryOccurrence_DuplicatesAndUnusablesIncluded()
    {
        var p = new TestPredicate();

        var (value, _) = p.Evaluate(Count(p.Id("genre", Ok("rock"), Ok("rock"), BadString)));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(3m) }));
    }

    [Test]
    public void Count_IsDefinite_WhateverItsArgument()
    {
        Assert.That(Count(new TestPredicate().Id<decimal>("x")).IsDefinite, Is.True);
    }

    [Test]
    public void Fallback_OfAbsent_IsTheDefault()
    {
        var p = new TestPredicate();

        var (value, _) = p.Evaluate(Fallback(p.Id<decimal>("x"), 7m));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(7m) }));
    }

    [Test]
    public void Fallback_ReplacesEachUnusable_KeepingCardinality_AndReportsNothing()
    {
        var p = new TestPredicate();

        var (value, reported) = p.Evaluate(Fallback(p.Id("x", Ok(1m), BadNumber, BadNumber), 0m));

        Assert.That(value.Occurrences, Is.EquivalentTo(new[] { Ok(1m), Ok(0m), Ok(0m) }));
        Assert.That(reported, Is.Empty);
    }

    [Test]
    public void Fallback_OfAllUsable_IsTheValueUnchanged()
    {
        var p = new TestPredicate();

        var (value, _) = p.Evaluate(Fallback(p.Id("x", Ok(1m), Ok(1m)), 0m));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(1m), Ok(1m) }));
    }

    [Test]
    public void Number_OfAbsent_IsAbsent()
    {
        var p = new TestPredicate();

        var (value, _) = p.Evaluate(p.Number(p.Id<string>("s")));

        Assert.That(value.IsAbsent, Is.True);
    }

    [Test]
    public void Number_ConvertsEachOccurrence_PropagatingUnusables_AndGivingFailuresItsOwnOrigin()
    {
        var p = new TestPredicate();
        var s = p.Id("s", Ok("5"), Ok("abc"), BadString);
        var number = p.Number(s);

        var (value, reported) = p.Evaluate(number);

        Assert.That(value.Occurrences, Is.EquivalentTo(new Occurrence<decimal>[]
        {
            Ok(5m), new Unusable<decimal>(number.Origin), new Unusable<decimal>(s.Origin),
        }));
        Assert.That(reported, Is.Empty);
    }

    [TestCase("12345678901234567890123456789012")]
    [TestCase("0.000000000000000000000000000001")]
    public void Number_OfValidSyntaxThatANumberCannotHold_IsUnusable_NotRounded(string text)
    {
        var p = new TestPredicate();
        var number = p.Number(p.Id("s", Ok(text)));

        var (value, _) = p.Evaluate(number);

        Assert.That(value.Occurrences, Is.EqualTo(new[] { new Unusable<decimal>(number.Origin) }));
    }

    [Test]
    public void Number_OfByteCountAndDuration_IsBytesAndSeconds()
    {
        var p = new TestPredicate();

        Assert.That(p.Evaluate(p.Number(Lit(new ByteCount(1433.6m)))).Value.Occurrences, Is.EqualTo(new[] { Ok(1433.6m) }));
        Assert.That(p.Evaluate(p.Number(Lit(new Duration(245)))).Value.Occurrences, Is.EqualTo(new[] { Ok(245m) }));
    }

    [TestCase("1.50", "1.5")]
    [TestCase(".5", "0.5")]
    [TestCase("+5", "5")]
    [TestCase("-0", "0")]
    public void String_OfNumber_IsCanonical(string literal, string expected)
    {
        NumberText.TryParse(literal, out var number);
        var p = new TestPredicate();

        var (value, _) = p.Evaluate(p.String(Lit(number)));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { Ok(expected) }));
    }

    [Test]
    public void String_PropagatesUnusables_WithTheirOrigin()
    {
        var p = new TestPredicate();
        var x = p.Id("x", Ok(1m), BadNumber);

        var (value, reported) = p.Evaluate(p.String(x));

        Assert.That(value.Occurrences, Is.EquivalentTo(new Occurrence<string>[] { Ok("1"), new Unusable<string>(x.Origin) }));
        Assert.That(reported, Is.Empty);
    }

    [Test]
    public void String_OfByteCountAndDuration_IsAPlainNumber()
    {
        var p = new TestPredicate();

        Assert.That(p.Evaluate(p.String(Lit(new ByteCount(1000)))).Value.Occurrences, Is.EqualTo(new[] { Ok("1000") }));
        Assert.That(p.Evaluate(p.String(Lit(new Duration(120)))).Value.Occurrences, Is.EqualTo(new[] { Ok("120") }));
    }

    [Test]
    public void Conversions_AreDefinite_WhenTheirArgumentIs()
    {
        var p = new TestPredicate();

        Assert.That(p.String(Lit(1m)).IsDefinite, Is.True);
        Assert.That(p.Number(p.Id<string>("s")).IsDefinite, Is.False);
        Assert.That(Fallback(Lit(1m), 0m).IsDefinite, Is.True);
        Assert.That(Fallback(p.Id<decimal>("x"), 0m).IsDefinite, Is.False);
    }

    private sealed class RecordingBinding : IdentifierBinding<decimal>
    {
        public List<FileData> Files { get; } = [];

        public override Value<decimal> Resolve(FileData file, Origin origin)
        {
            Files.Add(file);
            return Value<decimal>.Single(new Unusable<decimal>(origin));
        }
    }
}
