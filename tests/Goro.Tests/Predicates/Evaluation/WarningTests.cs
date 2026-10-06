using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>
/// What is reported while a predicate is evaluated: the deduplication table of the predicate
/// documentation row by row, what a warning quotes, and what reports nothing.
/// </summary>
/// <remarks>
/// As in the table, <c>x</c> and <c>y</c> resolve to an unusable occurrence, <c>m</c> is a
/// multivalue that includes one, and <c>s</c> and <c>t</c> hold strings that are not numbers.
/// </remarks>
public class WarningTests
{
    private TestPredicate p = null!;

    [SetUp]
    public void SetUp() => p = new TestPredicate();

    private Goro.Predicates.Evaluation.IdentifierReference<decimal> X => p.Id("x", BadNumber);

    private Goro.Predicates.Evaluation.IdentifierReference<decimal> Y => p.Id("y", BadNumber);

    [Test]
    public void Row_xGt1_One()
    {
        var x = X;

        Assert.That(p.Decide(Gt(x, Lit(1m))).Reported, Is.EqualTo(new[] { x.Origin }));
    }

    [Test]
    public void Row_xGtx_One_BothMentionsAreOneSource()
    {
        var x = X;

        Assert.That(p.Decide(Gt(x, x)).Reported, Is.EqualTo(new[] { x.Origin }));
    }

    [Test]
    public void Row_xLt10_Or_xGt20_One_TheSameSourceReachedTwice()
    {
        var x = X;

        var outcome = p.Decide(Or(Lt(x, Lit(10m)), Gt(x, Lit(20m))));

        Assert.That(outcome.Reported, Is.EqualTo(new[] { x.Origin }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
    }

    [Test]
    public void Row_xGty_Two()
    {
        var x = X;
        var y = Y;

        Assert.That(p.Decide(Gt(x, y)).Reported, Is.EqualTo(new[] { x.Origin, y.Origin }));
    }

    [Test]
    public void Row_mGt1_One_EveryOccurrenceSharesTheSource()
    {
        var m = p.Id("m", Ok(5m), BadNumber, BadNumber);

        var outcome = p.Decide(Gt(m, Lit(1m)));

        Assert.That(outcome.Reported, Is.EqualTo(new[] { m.Origin }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
    }

    [Test]
    public void Row_NumberOfsGtNumberOfs_One_StructurallyIdenticalSubExpressionsAreOneSource()
    {
        var s = p.Id("s", Ok("abc"));
        var left = p.Number(s);
        var right = p.Number(s);

        var outcome = p.Decide(Gt(left, right));

        Assert.That(outcome.Sources, Is.EqualTo(new[] { p.SourceOf("s AS NUMBER") }));
        Assert.That(outcome.Reported, Is.EqualTo(new[] { left.Origin }));
    }

    [Test]
    public void Row_NumberOfsGtNumberOft_Two_SourcesDifferBelowTheTopLevel()
    {
        var left = p.Number(p.Id("s", Ok("abc")));
        var right = p.Number(p.Id("t", Ok("def")));

        Assert.That(p.Decide(Gt(left, right)).Reported, Is.EqualTo(new[] { left.Origin, right.Origin }));
    }

    [Test]
    public void Row_AllOfmEqA_Or_mEqB_One_AllIsTransparent()
    {
        var m = p.Id("m", Ok("a"), BadString);

        var outcome = p.Decide(Or(Eq(All(m), Lit("a")), Eq(m, Lit("b"))));

        Assert.That(outcome.Reported, Is.EqualTo(new[] { m.Origin }));
    }

    [Test]
    public void Row_xGt1_And_yGt1_Two_AnUnusableFirstOperandDoesNotSettleAnd()
    {
        var x = X;
        var y = Y;

        var outcome = p.Decide(And(Gt(x, Lit(1m)), Gt(y, Lit(1m))));

        Assert.That(outcome.Reported, Is.EqualTo(new[] { x.Origin, y.Origin }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
    }

    [Test]
    public void Row_xGt1_Eq_xGt1_One_AnUnusableBooleanHasAlreadyBeenReported()
    {
        var x = X;

        var outcome = p.Decide(Eq(Gt(x, Lit(1m)), Gt(x, Lit(1m))));

        Assert.That(outcome.Reported, Is.EqualTo(new[] { x.Origin }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.Unusable));
    }

    [Test]
    public void Row_FallbackOf_xGt1_One_FallbackReplacesTheResultAfterTheComparisonWarned()
    {
        var x = X;

        var outcome = p.Decide(Fallback(Gt(x, Lit(1m)), false));

        Assert.That(outcome.Reported, Is.EqualTo(new[] { x.Origin }));
        Assert.That(outcome.Truth, Is.EqualTo(Truth.False));
    }

    [Test]
    public void FallbackOfx_Gt1_None()
    {
        var outcome = p.Decide(Gt(Fallback(X, 0m), Lit(1m)));

        Assert.That(outcome.Reported, Is.Empty);
        Assert.That(outcome.Truth, Is.EqualTo(Truth.False));
    }

    [Test]
    public void xGt1_IsUnusable_IsTrue_AndReportsOnlyWhatTheComparisonDid()
    {
        var x = X;

        var outcome = p.Decide(IsState(Gt(x, Lit(1m)), TestedState.Unusable));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Reported, Is.EqualTo(new[] { x.Origin }));
    }

    [Test]
    public void ASettlingFirstOperand_ShortCircuits_AndNothingIsReported()
    {
        var y = Y;

        Assert.That(p.Decide(And(False, Gt(y, Lit(1m)))).Reported, Is.Empty);
        Assert.That(p.Decide(Or(True, Gt(y, Lit(1m)))).Reported, Is.Empty);
    }

    // The guard idiom, and the same guard transposed: the observable difference is a warning.
    [Test]
    public void AGuard_ShieldsWhatFollowsIt_ButNotWhatPrecedesIt()
    {
        var s = p.Id("s", Ok("120"), Ok("junk"));
        var number = p.Number(s);
        var guard = IsState(All(number), TestedState.Usable);
        var comparison = Gt(number, Lit(100m));

        var guarded = p.Decide(And(guard, comparison));
        var transposed = p.Decide(And(comparison, guard));

        Assert.That(guarded.Truth, Is.EqualTo(Truth.False));
        Assert.That(guarded.Reported, Is.Empty);
        Assert.That(transposed.Truth, Is.EqualTo(Truth.False));
        Assert.That(transposed.Reported, Is.EqualTo(new[] { number.Origin }));
    }

    // Iteration within an operator does not short-circuit, so the warning does not depend on
    // whether the match was reached before the unusable occurrence.
    [Test]
    public void AMatchAmongUsableOccurrences_StillReportsTheUnusableOne()
    {
        var m = p.Id("m", Ok(1991m), BadNumber);

        var outcome = p.Decide(Eq(m, Lit(1991m)));

        Assert.That(outcome.Truth, Is.EqualTo(Truth.True));
        Assert.That(outcome.Reported, Is.EqualTo(new[] { m.Origin }));
    }

    [Test]
    public void TheSameBagInReverseOrder_GivesTheSameResultAndReports()
    {
        Occurrence<string>[] bag = [Ok("2"), Ok("junk"), BadString, Ok("x7")];
        Occurrence<string>[] reversed = [.. bag.Reverse()];

        Outcome Run(Occurrence<string>[] occurrences)
        {
            var q = new TestPredicate();
            return q.Decide(Gt(q.Number(q.Id("m", occurrences)), Lit(1m)));
        }

        var forwards = Run(bag);
        var backwards = Run(reversed);

        Assert.That(backwards.Truth, Is.EqualTo(forwards.Truth));
        Assert.That(backwards.Reported, Is.EqualTo(forwards.Reported));
        Assert.That(forwards.Quoted, Is.EqualTo(new[] { "m", "m AS NUMBER" }));
    }

    // Of the mentions of one source that reported in a file, a warning quotes the one written first,
    // whichever an operator happened to reach first. The second tree is built out of written order
    // to show that evaluation order plays no part.
    [Test]
    public void TheQuotedText_IsThatOfTheMentionWrittenFirst()
    {
        var x = X;
        var upper = p.Mention(x, "X", start: 4);

        Assert.That(p.Decide(Gt(x, upper)).Quoted, Is.EqualTo(new[] { "x" }));
        Assert.That(p.Decide(Gt(upper, x)).Quoted, Is.EqualTo(new[] { "x" }));
    }

    // A mention that was never evaluated reported nothing, so it is never the one quoted.
    [Test]
    public void TheQuotedText_IsNeverThatOfAMentionNotEvaluated()
    {
        var x = X;
        var upper = p.Mention(x, "X", start: 30);

        Assert.That(p.Decide(Or(And(False, Gt(x, Lit(1m))), Gt(upper, Lit(1m)))).Quoted, Is.EqualTo(new[] { "X" }));
    }

    // Within one operator, which mention reports first depends on the order of a bag, and nesting
    // m outside M, a bag of a usable and an unusable occurrence has M report first. What is quoted
    // must not depend on it.
    [TestCase(false)]
    [TestCase(true)]
    public void TheQuotedText_DoesNotDependOnTheOrderOfABag(bool reversed)
    {
        Occurrence<decimal>[] bag = [Ok(1m), BadNumber];
        var m = p.Id("m", reversed ? [.. bag.Reverse()] : bag);
        var upper = p.Mention(m, "M", start: 5);

        Assert.That(p.Decide(Eq(m, upper)).Quoted, Is.EqualTo(new[] { "m" }));
    }

    [Test]
    public void TheQuotedText_OfAConversion_IsAsWritten()
    {
        var s = p.Id("s", Ok("abc"));
        var written = p.Number(p.Mention(s, "S", start: 0), "number( S )");

        Assert.That(p.Decide(Gt(written, p.Number(s))).Quoted, Is.EqualTo(new[] { "number( S )" }));
    }

    // The universal operand is iterated first, and the mention written first is still the one quoted.
    [Test]
    public void TheQuotedText_UnderAUniversalQuantifier_IsThatOfTheMentionWrittenFirst()
    {
        var x = X;
        var upper = p.Mention(x, "X", start: 5);

        Assert.That(p.Decide(Eq(x, All(upper))).Quoted, Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void AnOperatorsUnusableResult_HasNoOrigin()
    {
        var (value, _) = p.Evaluate(Gt(X, Lit(1m)));

        Assert.That(value.Occurrences, Is.EqualTo(new[] { new Unusable<bool>(null) }));
    }
}
