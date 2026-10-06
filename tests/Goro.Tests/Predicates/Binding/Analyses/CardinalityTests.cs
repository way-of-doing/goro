using Goro.Predicates.Binding;
using Goro.Tests.Predicates.Binding.Support;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Binding.Analyses;

/// <summary>
/// Definiteness, asserted directly, and the rules that read it. As in the standard test catalog,
/// <c>one</c> is the only definite identifier; every other is not.
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
    public void Definiteness(string text, string written, bool definite)
    {
        var tree = Analyse.Bind(text);

        Assert.That(Cardinality.Analyse(tree).IsDefinite(tree.Find(written)), Is.EqualTo(definite));
    }

    [TestCase("artst == 1", "artst")]
    [TestCase("FALLBACK(artst, 1) == 1", "FALLBACK(artst, 1)")]
    public void TheErrorType_IsDefinite(string text, string written)
    {
        var tree = Analyse.Bind(text);

        Assert.That(Cardinality.Analyse(tree).IsDefinite(tree.Find(written)), Is.True);
    }

    [Test]
    public void FallbackWithTheWrongNumberOfArguments_IsStillAsDefiniteAsItsArgument()
    {
        var tree = Analyse.Bind("FALLBACK(x) > 1");

        Assert.That(Cardinality.Analyse(tree).IsDefinite(tree.Find("FALLBACK(x)")), Is.False);
    }

    [TestCase("flag", new[] { Codes.IndefiniteCondition })]
    [TestCase("NOT (flag)", new[] { Codes.IndefiniteCondition })]
    [TestCase("flag OR flag", new[] { Codes.IndefiniteCondition, Codes.IndefiniteCondition })]
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
        Assert.That(Cardinality.Analyse(tree).Diagnostics.Codes(), Is.EqualTo(new[] { Codes.IndefiniteCondition }));
    }
}
