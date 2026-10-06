using Goro.Predicates.Binding;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Text;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Binding;

/// <summary>
/// What the binder makes of a predicate: names resolved, types assigned, parentheses and modifiers
/// folded away, and only its own rules reported.
/// </summary>
public class SemanticTreeTests
{
    [Test]
    public void Parentheses_LeaveNothingBehind()
    {
        var tree = Analyse.Bind("((x)) > (1)");

        var comparison = (SemanticComparison)tree.Root;
        Assert.That(comparison.Left.Expression, Is.SameAs(tree.Find("x")));
        Assert.That(comparison.Right.Expression, Is.SameAs(tree.Find("1")));
    }

    [TestCase("ALL(genre) == \"a\"", Quantifier.Universal)]
    [TestCase("ANY(genre) == \"a\"", Quantifier.Existential)]
    [TestCase("genre == \"a\"", Quantifier.Existential)]
    [TestCase("(ALL((genre))) == \"a\"", Quantifier.Universal)]
    public void Quantifier_IsFoldedIntoItsOperand(string text, Quantifier quantifier)
    {
        var tree = Analyse.Bind(text);

        var left = ((SemanticComparison)tree.Root).Left;
        Assert.That(left.Quantifier, Is.EqualTo(quantifier));
        Assert.That(left.Expression, Is.InstanceOf<SemanticIdentifier>());
    }

    [TestCase("genre == \"a\"", ComparisonMode.Normalized)]
    [TestCase("LITERALLY(genre) == \"a\"", ComparisonMode.Literal)]
    [TestCase("genre == LITERALLY(\"a\")", ComparisonMode.Literal)]
    [TestCase("ALL(LITERALLY(genre)) == \"a\"", ComparisonMode.Literal)]
    public void Literally_IsFoldedIntoItsOperatorsMode(string text, ComparisonMode mode)
    {
        Assert.That(((SemanticComparison)Analyse.Bind(text).Root).Mode, Is.EqualTo(mode));
    }

    [TestCase("file::size > 10", "10", GoroType.ByteCount)]
    [TestCase("10 < file::duration", "10", GoroType.Duration)]
    [TestCase("file::size BETWEEN 1..5", "5", GoroType.ByteCount)]
    [TestCase("FALLBACK(file::duration, 5) > 1m", "5", GoroType.Duration)]
    public void NumberLiteral_WhereAUnitIsWanted_StandsForIt(string text, string literal, GoroType type)
    {
        var node = Analyse.Bind(text).Find<SemanticLiteral>(literal);

        Assert.That(node.Type, Is.EqualTo(type));
        Assert.That(node.StandsIn, Is.True);
    }

    [TestCase("x > 10")]
    [TestCase("file::size > 10kb")]
    public void Literal_OtherwiseHasItsOwnType(string text)
    {
        var node = (SemanticLiteral)((SemanticComparison)Analyse.Bind(text).Root).Right.Expression;

        Assert.That(node.StandsIn, Is.False);
    }

    [Test]
    public void ConversionToItsOwnType_IsKept_SoThatItIsNoLiteral()
    {
        var tree = Analyse.Bind("NUMBER(5) > 1");

        var conversion = tree.Find<SemanticConversion>("NUMBER(5)");
        Assert.That(conversion.IsIdentity, Is.True);
        Assert.That(conversion.Argument, Is.InstanceOf<SemanticLiteral>());
    }

    [Test]
    public void UnknownName_IsTheErrorType_InAWholeTree()
    {
        var tree = Analyse.Bind("artst == \"a\" AND x > 1");

        Assert.That(tree.Find("artst").IsError, Is.True);
        Assert.That(tree.Find("artst == \"a\"").Type, Is.EqualTo(GoroType.Boolean));
        Assert.That(tree.Find("x > 1"), Is.InstanceOf<SemanticComparison>());
        Assert.That(tree.Diagnostics.Codes(), Is.EqualTo(new[] { Codes.UnknownIdentifier }));
    }

    [TestCase("COUNT(1, NUMBER(\"x\")) > 1", "NUMBER(\"x\")")]
    [TestCase("NUMBR(NUMBER(\"x\")) > 1", "NUMBER(\"x\")")]
    [TestCase("NUMBER(\"x\") == NULL", "NUMBER(\"x\")")]
    public void WhatARejectedExpressionContains_IsStillInTheTree(string text, string inside)
    {
        Assert.That(Analyse.Bind(text).Find(inside), Is.InstanceOf<SemanticConversion>());
    }

    // Each of these breaks a rule an analysis owns, which the binder leaves to it.
    [TestCase("flag")]
    [TestCase("genre != \"a\"")]
    [TestCase("NUMBER(\"x\") > 1")]
    [TestCase("FALLBACK(x, y) > 1")]
    [TestCase("file::size > -1")]
    [TestCase("artist BETWEEN \"b\"..\"a\"")]
    [TestCase("genre =~ r\"(\"")]
    public void RulesOwnedByAnAnalysis_AreNotTheBinders(string text)
    {
        Assert.That(Analyse.Bind(text).Diagnostics, Is.Empty);
    }
}
