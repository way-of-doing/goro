using Goro.Predicates;
using Goro.Predicates.Analyses;
using Goro.Predicates.Binding;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Lowering;
using Goro.Predicates.Text;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;

namespace Goro.Tests.Predicates.Lowering;

/// <summary>
/// Lowering a sound semantic tree to the evaluation tree: each node becomes what it stands for, and
/// what an analysis computed is taken from it rather than computed again.
/// </summary>
public class LoweringTests
{
    [Test]
    public void ConversionToItsOwnType_LeavesNothingBehind()
    {
        var comparison = (ComparisonTest<decimal>)Lower("x AS NUMBER > 1").Root;

        Assert.That(comparison.Left.Expression, Is.InstanceOf<IdentifierReference<decimal>>());
    }

    [Test]
    public void NumberStandingForAUnit_BecomesALiteralOfThatUnit()
    {
        var comparison = (ComparisonTest<ByteCount>)Lower("file::size > 10").Root;

        Assert.That(((Literal<ByteCount>)comparison.Right.Expression).Value, Is.EqualTo(new ByteCount(10)));
    }

    [Test]
    public void Pattern_IsTheOneThePatternsAnalysisCompiled()
    {
        var tree = Analyse.Bind("genre =~ r\"a+\"");
        var patterns = Patterns.Analyse(tree);

        var match = (RegexMatch)Lowerer.Lower(tree, Constants.Analyse(tree), patterns, Sources.Analyse(tree)).Root;

        Assert.That(match.Pattern, Is.SameAs(patterns.PatternOf((SemanticMatch)tree.Root)));
    }

    // Preparing this string in normalized mode is not idempotent (see implementation.md), so a range
    // end prepared by the constants analysis and again by lowering would come out different.
    [Test]
    public void RangeEnds_ArePreparedOnce()
    {
        const string end = "कͅा";
        var once = Normalization.Prepare(end, ComparisonMode.Normalized);
        Assume.That(Normalization.Prepare(once, ComparisonMode.Normalized), Is.Not.EqualTo(once));

        var range = (RangeTest<string>)Lower($"artist BETWEEN \"a\"..\"{end}\"").Root;

        Assert.That(range.Maximum, Is.EqualTo(once));
    }

    private static CompiledPredicate Lower(string text)
    {
        var tree = Analyse.Bind(text);
        Assert.That(tree.Diagnostics, Is.Empty);
        return Lowerer.Lower(tree, Constants.Analyse(tree), Patterns.Analyse(tree), Sources.Analyse(tree));
    }
}
