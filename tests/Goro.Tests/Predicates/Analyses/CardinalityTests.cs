using Goro.Predicates.Analyses;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Analyses;

/// <summary>
/// Bounds, asserted directly, and the rules that read them. As in the standard test catalog,
/// <c>one</c> is the only identifier that is exactly one; every other can be absent or several.
/// </summary>
public class CardinalityTests
{
    [TestCase("x > 1", "x", false)]
    [TestCase("one == \"a\"", "one", true)]
    [TestCase("x > 1", "1", true)]
    [TestCase("COUNT(genre) > 1", "COUNT(genre)", true)]
    [TestCase("NUMBER(genre) > 1", "NUMBER(genre)", false)]
    [TestCase("NUMBER(one) > 1", "NUMBER(one)", true)]
    [TestCase("NUMBER(x) > 1", "NUMBER(x)", false)]
    [TestCase("FALLBACK(x, 0) > 1", "FALLBACK(x, 0)", false)]
    [TestCase("FALLBACK(one, \"\") == \"a\"", "FALLBACK(one, \"\")", true)]
    [TestCase("COUNT(x > 1) > 1", "x > 1", true)]
    [TestCase("NOT flag", "NOT flag", true)]
    [TestCase("FALLBACK(file::extension, \"\") == \"a\"", "FALLBACK(file::extension, \"\")", true)]
    [TestCase("file::duration > 1", "file::duration", true)]
    [TestCase("PREFERRED(file::extension, \"x\") == \"a\"", "PREFERRED(file::extension, \"x\")", true)]
    [TestCase("PREFERRED(file::extension, file::extension) == \"a\"", "PREFERRED(file::extension, file::extension)", false)]
    public void ExactlyOne(string text, string written, bool exactlyOne)
    {
        var tree = Analyse.Bind(text);

        Assert.That(Cardinality.Analyse(tree).BoundsOf(tree.Find(written)) == Bounds.ExactlyOne, Is.EqualTo(exactlyOne));
    }

    // The static judgements of evaluation.md, written as l..u.
    [TestCase("x > 1", "x", "0..many")]
    [TestCase("file::extension == \"a\"", "file::extension", "0..1")]
    [TestCase("id3v1::year > 1", "id3v1::year", "0..1")]
    [TestCase("year > 1", "year", "0..many")]
    [TestCase("x > 1", "1", "1..1")]
    [TestCase("NUMBER(file::extension) > 1", "NUMBER(file::extension)", "0..1")]
    [TestCase("FALLBACK(x, 0) > 1", "FALLBACK(x, 0)", "1..many")]
    [TestCase("FALLBACK(id3v1::year, 0) > 1", "FALLBACK(id3v1::year, 0)", "1..1")]
    [TestCase("PREFERRED(x, 1) > 1", "PREFERRED(x, 1)", "1..many")]
    [TestCase("PREFERRED(id3v1::year, file::extension) == \"a\"", "PREFERRED(id3v1::year, file::extension)", "0..1")]
    [TestCase("PREFERRED(id3v1::year, x) > 1", "PREFERRED(id3v1::year, x)", "0..many")]
    [TestCase("COUNT(x) > 1", "COUNT(x)", "1..1")]
    public void BoundsOfEachConstruct(string text, string written, string bounds)
    {
        var tree = Analyse.Bind(text);

        Assert.That(Cardinality.Analyse(tree).BoundsOf(tree.Find(written)).ToString(), Is.EqualTo(bounds));
    }

    [TestCase("artst == 1", "artst")]
    [TestCase("FALLBACK(artst, 1) == 1", "FALLBACK(artst, 1)")]
    public void TheErrorType_IsExactlyOne(string text, string written)
    {
        var tree = Analyse.Bind(text);

        Assert.That(Cardinality.Analyse(tree).BoundsOf(tree.Find(written)), Is.EqualTo(Bounds.ExactlyOne));
    }

    [Test]
    public void FallbackWithTheWrongNumberOfArguments_StillHasItsArgumentsBounds_MadePresent()
    {
        var tree = Analyse.Bind("FALLBACK(x) > 1");

        Assert.That(Cardinality.Analyse(tree).BoundsOf(tree.Find("FALLBACK(x)")).ToString(), Is.EqualTo("1..many"));
    }

    [TestCase("flag", new[] { Codes.ConditionNotExactlyOne })]
    [TestCase("NOT (flag)", new[] { Codes.ConditionNotExactlyOne })]
    [TestCase("flag OR flag", new[] { Codes.ConditionNotExactlyOne, Codes.ConditionNotExactlyOne })]
    [TestCase("genre != \"a\"", new[] { Codes.AmbiguousNotEqual })]
    [TestCase("ALL(genre) != \"a\"", new string[0])]
    [TestCase("one != \"a\"", new string[0])]
    [TestCase("genre == \"a\"", new string[0])]
    [TestCase("artst != \"a\"", new string[0])]
    [TestCase("artist AND x > 1", new string[0])]
    public void Rules(string text, string[] codes)
    {
        Assert.That(Cardinality.Analyse(Analyse.Bind(text)).Diagnostics.Codes(), Is.EqualTo(codes));
    }

    [Test]
    public void ATreeWithErrors_StillHasItsOwnChecked()
    {
        var tree = Analyse.Bind("artst == 1 AND flag");

        Assert.That(tree.Diagnostics.Codes(), Is.EqualTo(new[] { Codes.UnknownIdentifier }));
        Assert.That(Cardinality.Analyse(tree).Diagnostics.Codes(), Is.EqualTo(new[] { Codes.ConditionNotExactlyOne }));
    }
}
