using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;
using static Goro.Tests.Predicates.Support.CompileAssert;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>
/// Warning sources, interned from text: structural identity, whatever the spelling, and the
/// deduplication table of the predicate documentation row by row.
/// </summary>
/// <remarks>
/// As in the table, <c>x</c> and <c>y</c> resolve to an unusable occurrence, <c>m</c> is a
/// multivalue that includes one, and <c>s</c> and <c>t</c> hold strings that are not numbers.
/// </remarks>
public class SourceTests
{
    private static TestCatalog Table() => TestCatalog.Standard()
        .With("x", BadNumber)
        .With("y", BadNumber)
        .With("m", Ok("a"), BadString)
        .With("s", Ok("one"))
        .With("t", Ok("two"));

    [TestCase("x > 1", new[] { "x" })]
    [TestCase("x > 1 AND s AS NUMBER > 1", new[] { "x", "s", "s AS NUMBER" })]
    [TestCase("s AS NUMBER > t AS NUMBER", new[] { "s", "s AS NUMBER", "t", "t AS NUMBER" })]
    [TestCase("COUNT(genre) > 1", new[] { "genre" })]
    [TestCase("x > 1 AND TRUE", new[] { "x" })]
    [TestCase("FALLBACK(s, \"0\") AS NUMBER > 1", new[] { "s", "FALLBACK(s, \"0\") AS NUMBER" })]
    [TestCase("COUNT(x > 1) AS STRING == \"1\"", new[] { "x", "COUNT(x > 1) AS STRING" })]
    [TestCase("ape::field(\"Album Artist\") == \"x\"", new[] { "ape::field(\"album artist\")" })]
    [TestCase("id3v2::field(\"TXXX\", \"MOOD\") == \"x\" OR id3v2::field(\"TXXX\") == \"x\"", new[] { "id3v2::field(\"txxx\", \"mood\")", "id3v2::field(\"txxx\")" })]
    [TestCase("id3v2::field(\"TPE1\") == \"x\" OR id3v2::artist == \"x\" OR artist == \"x\"", new[] { "id3v2::field(\"tpe1\")", "id3v2::artist", "artist" })]
    [TestCase("1 == 1", new string[0])]
    public void SourceTable_ListsEveryDistinctSource_ChildrenFirst(string text, string[] forms)
    {
        Assert.That(Compiles(text).Sources.CanonicalForms, Is.EqualTo(forms));
    }

    // Case, whitespace, the string form of a name, parentheses and modifiers never make two sources,
    // and nor does the case of a name a source function matches without regard to case.
    [TestCase("x > 1 OR X > 2 OR ( x ) > 4", new[] { "x" })]
    [TestCase("s AS NUMBER >  S  AS NUMBER", new[] { "s", "s AS NUMBER" })]
    [TestCase("s AS NUMBER > 1 OR S AS number > 1", new[] { "s", "s AS NUMBER" })]
    [TestCase("ALL(m) == \"a\" OR LITERALLY(m) == \"b\" OR (ANY(m)) == \"c\"", new[] { "m" })]
    [TestCase("file::size > 1 AND FILE::Size > 2", new[] { "file::size" })]
    [TestCase("vorbis::field(\"BPM\") == \"1\" OR VORBIS::Field(\"bpm\") == \"2\" OR vorbis::field(r\"Bpm\") == \"3\"", new[] { "vorbis::field(\"bpm\")" })]
    [TestCase("id3v2::bytes(\"apic\") IS ABSENT OR id3v2::bytes(\"APIC\") IS USABLE", new[] { "id3v2::bytes(\"apic\")" })]
    [TestCase("FALLBACK(s, \"0\") AS NUMBER > FALLBACK(s, r\"0\") AS NUMBER", new[] { "s", "FALLBACK(s, \"0\") AS NUMBER" })]
    [TestCase("FALLBACK(s, \"0\") AS NUMBER > FALLBACK(s, \"00\") AS NUMBER", new[] { "s", "FALLBACK(s, \"0\") AS NUMBER", "FALLBACK(s, \"00\") AS NUMBER" })]
    public void SpellingsOfOneSubExpression_AreOneSource(string text, string[] forms)
    {
        Assert.That(Compiles(text).Sources.CanonicalForms, Is.EqualTo(forms));
    }

    [Test]
    public void QuotedAndRawString_AreOneValue_AndOneSource()
    {
        const string text = @"FALLBACK(s, ""a\\b"") AS NUMBER > 1 AND FALLBACK(s, r""a\b"") AS NUMBER > 1";
        var catalog = TestCatalog.Standard();

        var outcome = Evaluate(text, catalog);

        Assert.That(Compiles(text).Sources.CanonicalForms, Is.EqualTo(new[] { "s", @"FALLBACK(s, ""a\\b"") AS NUMBER" }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { @"FALLBACK(s, ""a\\b"") AS NUMBER" }));
        Assert.That(Evaluate(@"""a\\b"" == r""a\b""", catalog).Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void Origin_QuotesTheTextOfTheMentionThatProducedTheValue()
    {
        var outcome = Evaluate("X > 1", Table());

        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "X" }));
    }

    // With m nested outside M, the bag in its first order has M report first; the spelling written
    // first is quoted either way.
    [TestCase(false)]
    [TestCase(true)]
    public void Origin_OfASourceWrittenTwice_IsTheSpellingWrittenFirst(bool reversed)
    {
        Occurrence<string>[] bag = [Ok("a"), BadString];
        var catalog = TestCatalog.Standard().With("m", reversed ? [.. bag.Reverse()] : bag);

        Assert.That(Evaluate("m == M", catalog).Quoted, Is.EqualTo(new[] { "m" }));
    }

    [TestCase("lab::q > 1 OR LAB::Q > 2", "lab::q")]
    [TestCase("LAB::Q > 1 OR lab::q > 2", "LAB::Q")]
    public void QualifiedNames_InAnyCase_AreOneIdentifierAndOneSource(string text, string quoted)
    {
        var catalog = TestCatalog.Standard().With("lab::q", BadNumber);

        var outcome = Evaluate(text, catalog);

        Assert.That(Compiles(text, catalog).Sources.CanonicalForms, Is.EqualTo(new[] { "lab::q" }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { quoted }));
    }

    [Test]
    public void Origin_OfAConversion_QuotesTheConversionAsWritten()
    {
        var outcome = Evaluate("s  as  number > 1", Table());

        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "s  as  number" }));
    }

    [Test]
    public void Warning_NamesTheSourceThroughAPropagatingFunction()
    {
        var outcome = Evaluate("x AS STRING == \"1991\"", Table());

        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "x" }));
    }

    [TestCase("x > 1", 1)]
    [TestCase("x > x", 1)]
    [TestCase("x < 10 OR x > 20", 1)]
    [TestCase("x > y", 2)]
    [TestCase("m > \"1\"", 1)]
    [TestCase("s AS NUMBER > s AS NUMBER", 1)]
    [TestCase("s AS NUMBER > t AS NUMBER", 2)]
    [TestCase("ALL(m) == \"a\" OR m == \"b\"", 1)]
    [TestCase("x > 1 AND y > 1", 2)]
    [TestCase("(x > 1) == (x > 1)", 1)]
    [TestCase("FALLBACK(x > 1, FALSE)", 1)]
    public void DeduplicationTable_Row(string text, int warnings)
    {
        Assert.That(Evaluate(text, Table()).Reported, Has.Count.EqualTo(warnings));
    }
}
