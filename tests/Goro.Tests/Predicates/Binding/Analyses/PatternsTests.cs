using System.Text.RegularExpressions;
using Goro.Predicates.Binding;
using Goro.Tests.Predicates.Binding.Support;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Binding.Analyses;

/// <summary>Every pattern compiled once, in its operator's mode, or reported.</summary>
public class PatternsTests
{
    [TestCase("genre =~ r\"a+\"", true)]
    [TestCase("LITERALLY(genre) =~ r\"a+\"", false)]
    public void Pattern_IsCompiledInTheOperatorsMode(string text, bool ignoresCase)
    {
        var tree = Analyse.Bind(text);

        var pattern = Patterns.Analyse(tree).PatternOf((SemanticMatch)tree.Root);

        Assert.That(pattern, Is.Not.Null);
        Assert.That(pattern!.Options.HasFlag(RegexOptions.IgnoreCase), Is.EqualTo(ignoresCase));
        Assert.That(pattern.Options.HasFlag(RegexOptions.NonBacktracking), Is.True);
    }

    [TestCase("genre =~ r\"(\"", Codes.InvalidPattern)]
    [TestCase("genre =~ r\"(?=a)\"", Codes.UnsupportedPatternConstruct)]
    public void UnsoundPattern_IsReported_AndHasNoCompiledForm(string text, string code)
    {
        var tree = Analyse.Bind(text);

        var analysis = Patterns.Analyse(tree);

        Assert.That(analysis.Diagnostics.Codes(), Is.EqualTo(new[] { code }));
        Assert.That(analysis.PatternOf((SemanticMatch)tree.Root), Is.Null);
    }

    [Test]
    public void APatternIsChecked_WhateverIsWrongWithItsSubject()
    {
        var tree = Analyse.Bind("x =~ r\"(\"");

        Assert.That(tree.Diagnostics.Codes(), Is.EqualTo(new[] { Codes.MatchSubjectNotString }));
        Assert.That(Patterns.Analyse(tree).Diagnostics.Codes(), Is.EqualTo(new[] { Codes.InvalidPattern }));
    }
}
