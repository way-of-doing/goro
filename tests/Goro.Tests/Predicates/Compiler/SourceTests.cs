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
    [TestCase("x > 1 AND NUMBER(s) > 1", new[] { "x", "s", "NUMBER(s)" })]
    [TestCase("NUMBER(s) > NUMBER(t)", new[] { "s", "NUMBER(s)", "t", "NUMBER(t)" })]
    [TestCase("COUNT(genre) > 1", new[] { "genre" })]
    [TestCase("x > 1 AND TRUE", new[] { "x" })]
    [TestCase("NUMBER(FALLBACK(s, \"0\")) > 1", new[] { "s", "NUMBER(FALLBACK(s, \"0\"))" })]
    [TestCase("STRING(COUNT(x > 1)) == \"1\"", new[] { "x", "STRING(COUNT(x > 1))" })]
    [TestCase("ape::\"Album Artist\" == \"x\"", new[] { "ape::\"album artist\"" })]
    [TestCase("1 == 1", new string[0])]
    public void SourceTable_ListsEveryDistinctSource_ChildrenFirst(string text, string[] forms)
    {
        Assert.That(Compiles(text).Sources.CanonicalForms, Is.EqualTo(forms));
    }

    // Case, whitespace, an explicit namespace, parentheses and modifiers never make two sources.
    [TestCase("x > 1 OR X > 2 OR ::x > 3 OR ( x ) > 4 OR ::\"X\" > 5", new[] { "x" })]
    [TestCase("NUMBER(s) > NUMBER( S )", new[] { "s", "NUMBER(s)" })]
    [TestCase("NUMBER(s) > 1 OR number(::S) > 1", new[] { "s", "NUMBER(s)" })]
    [TestCase("ALL(m) == \"a\" OR LITERALLY(m) == \"b\" OR (ANY(m)) == \"c\"", new[] { "m" })]
    [TestCase("file::size > 1 AND ::FILE::\"size\" > 2", new[] { "file::size" })]
    [TestCase("vorbis::BPM == \"1\" OR vorbis::bpm == \"2\" OR vorbis::\"Bpm\" == \"3\"", new[] { "vorbis::bpm" })]
    [TestCase("NUMBER(FALLBACK(s, \"0\")) > NUMBER(FALLBACK(s, r\"0\"))", new[] { "s", "NUMBER(FALLBACK(s, \"0\"))" })]
    [TestCase("NUMBER(FALLBACK(s, \"0\")) > NUMBER(FALLBACK(s, \"00\"))", new[] { "s", "NUMBER(FALLBACK(s, \"0\"))", "NUMBER(FALLBACK(s, \"00\"))" })]
    public void SpellingsOfOneSubExpression_AreOneSource(string text, string[] forms)
    {
        Assert.That(Compiles(text).Sources.CanonicalForms, Is.EqualTo(forms));
    }

    [Test]
    public void QuotedAndRawString_AreOneValue_AndOneSource()
    {
        const string text = @"NUMBER(FALLBACK(s, ""a\\b"")) > 1 AND NUMBER(FALLBACK(s, r""a\b"")) > 1";
        var catalog = TestCatalog.Standard();

        var outcome = Evaluate(text, catalog);

        Assert.That(Compiles(text).Sources.CanonicalForms, Is.EqualTo(new[] { "s", @"NUMBER(FALLBACK(s, ""a\\b""))" }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { @"NUMBER(FALLBACK(s, ""a\\b""))" }));
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

    // A leading "::" starts a name at the global namespace, where every namespace sits.
    [TestCase("lab::q > 1 OR ::lab::q > 2", "lab::q")]
    [TestCase("::lab::q > 1 OR lab::q > 2", "::lab::q")]
    [TestCase("::LAB::\"Q\" > 1 OR lab::q > 2", "::LAB::\"Q\"")]
    public void RootedAndUnrootedQualifiedNames_AreOneIdentifierAndOneSource(string text, string quoted)
    {
        var catalog = TestCatalog.Standard().With("lab::q", BadNumber);

        var outcome = Evaluate(text, catalog);

        Assert.That(Compiles(text, catalog).Sources.CanonicalForms, Is.EqualTo(new[] { "lab::q" }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
        Assert.That(outcome.Quoted, Is.EqualTo(new[] { quoted }));
    }

    [Test]
    public void Origin_OfAConversion_QuotesTheCallAsWritten()
    {
        var outcome = Evaluate("number( s ) > 1", Table());

        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "number( s )" }));
    }

    [Test]
    public void Warning_NamesTheSourceThroughAPropagatingFunction()
    {
        var outcome = Evaluate("STRING(x) == \"1991\"", Table());

        Assert.That(outcome.Quoted, Is.EqualTo(new[] { "x" }));
    }

    [TestCase("x > 1", 1)]
    [TestCase("x > x", 1)]
    [TestCase("x < 10 OR x > 20", 1)]
    [TestCase("x > y", 2)]
    [TestCase("m > \"1\"", 1)]
    [TestCase("NUMBER(s) > NUMBER(s)", 1)]
    [TestCase("NUMBER(s) > NUMBER(t)", 2)]
    [TestCase("ALL(m) == \"a\" OR m == \"b\"", 1)]
    [TestCase("x > 1 AND y > 1", 2)]
    [TestCase("(x > 1) == (x > 1)", 1)]
    [TestCase("FALLBACK(x > 1, FALSE)", 1)]
    public void DeduplicationTable_Row(string text, int warnings)
    {
        Assert.That(Evaluate(text, Table()).Reported, Has.Count.EqualTo(warnings));
    }
}
