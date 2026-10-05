using Goro.Predicates.Binding;
using Goro.Predicates.Text;
using static Goro.Tests.Predicates.Binding.Support.BindAssert;
using Codes = Goro.Predicates.Binding.BinderDiagnosticCodes;

namespace Goro.Tests.Predicates.Binding;

/// <summary>
/// Modifiers: folded into the operator that reads them, a quantifier per operand and a mode per
/// operator, and allowed on an operand of a comparison, range, regex or state test and nowhere else.
/// </summary>
public class ModifierBindingTests
{
    [TestCase("ALL(genre) == \"x\"")]
    [TestCase("(ALL(genre)) == \"x\"")]
    [TestCase("ALL((genre)) == \"x\"")]
    [TestCase("ALL(ALL(genre)) == \"x\"")]
    [TestCase("LITERALLY(ALL(genre)) == \"x\"")]
    [TestCase("ALL(LITERALLY(genre)) == \"x\"")]
    public void Quantifier_IsTheOperands(string text)
    {
        var comparison = (ComparisonTest<string>)Compiles(text).Root;

        Assert.That(comparison.Left.Quantifier, Is.EqualTo(Quantifier.Universal));
        Assert.That(comparison.Right.Quantifier, Is.EqualTo(Quantifier.Existential));
    }

    [TestCase("genre == \"x\"", Quantifier.Existential)]
    [TestCase("ANY(genre) == \"x\"", Quantifier.Existential)]
    [TestCase("ALL(ANY(genre)) == \"x\"", null)]
    public void Quantifier_DefaultsToExistential(string text, Quantifier? expected)
    {
        if (expected is null)
        {
            var error = Error(text);
            Assert.That(error.Code, Is.EqualTo(Codes.ContradictoryQuantifiers));
            Assert.That(Marked(text, error), Is.EqualTo("ANY(genre)"));
            return;
        }

        Assert.That(((ComparisonTest<string>)Compiles(text).Root).Left.Quantifier, Is.EqualTo(expected));
    }

    [TestCase("ANY(LITERALLY(ALL(genre))) == \"x\"")]
    [TestCase("ALL(ANY(ANY(genre))) == \"x\"")]
    public void ContradictoryQuantifiers_AreOneError(string text)
    {
        Assert.That(Codes(text), Is.EqualTo(new[] { Codes.ContradictoryQuantifiers }));
    }

    [TestCase("genre == \"x\"", ComparisonMode.Normalized)]
    [TestCase("LITERALLY(genre) == \"x\"", ComparisonMode.Literal)]
    [TestCase("genre == LITERALLY(\"x\")", ComparisonMode.Literal)]
    [TestCase("LITERALLY(genre) == LITERALLY(\"x\")", ComparisonMode.Literal)]
    [TestCase("ALL(LITERALLY(genre)) == \"x\"", ComparisonMode.Literal)]
    public void Literally_OnEitherOperand_SetsTheOperatorsMode(string text, ComparisonMode mode)
    {
        var comparison = (ComparisonTest<string>)Compiles(text).Root;

        Assert.That(comparison.Order, Is.SameAs(StringOrder.For(mode)));
    }

    [Test]
    public void ParenthesizedModifiedOperand_IsTheSamePredicate()
    {
        var parenthesized = (ComparisonTest<string>)Compiles("(ALL(genre)) == \"x\"").Root;
        var plain = (ComparisonTest<string>)Compiles("ALL(genre) == \"x\"").Root;

        Assert.That(parenthesized.Left.Quantifier, Is.EqualTo(plain.Left.Quantifier));
        Assert.That(parenthesized.Left.Expression, Is.TypeOf(plain.Left.Expression.GetType()));
        Assert.That(parenthesized.Order, Is.SameAs(plain.Order));
    }

    // COUNT((ALL(genre))) is rejected exactly as COUNT(ALL(genre)) is: parentheses only group.
    [TestCase("COUNT(ALL(genre)) > 1", "ALL(genre)", "COUNT(genre) > 1")]
    [TestCase("COUNT((ALL(genre))) > 1", "ALL(genre)", "COUNT(genre) > 1")]
    [TestCase("COUNT(ALL(ANY(genre))) > 1", "ALL(ANY(genre))", "COUNT(genre) > 1")]
    [TestCase("FALLBACK(LITERALLY(genre), \"pop\") == \"Pop\"", "LITERALLY(genre)", "FALLBACK(genre, \"pop\") == \"Pop\"")]
    [TestCase("NUMBER(ANY(s)) > 1", "ANY(s)", "NUMBER(s) > 1")]
    [TestCase("ALL((x == 1))", "ALL((x == 1))", "(x == 1)")]
    [TestCase("x == 1 AND ANY((y == 1))", "ANY((y == 1))", "x == 1 AND (y == 1)")]
    [TestCase("NOT ALL((x == 1))", "ALL((x == 1))", "NOT (x == 1)")]
    [TestCase("(LITERALLY((x == 1)))", "LITERALLY((x == 1))", "(x == 1)")]
    public void Modifier_AnywhereButOnAnOperand_IsAnError(string text, string marked, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.MisplacedModifier));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void MisplacedModifier_DoesNotHideMistakesInsideIt()
    {
        Assert.That(Codes("COUNT(ALL(gnere)) > 1"), Is.EqualTo(new[] { Codes.MisplacedModifier, Codes.UnknownIdentifier }));
    }

    [Test]
    public void MisplacedModifiedRoot_ThatIsNoCondition_IsTwoErrors()
    {
        Assert.That(Codes("ALL(x)"), Is.EqualTo(new[] { Codes.MisplacedModifier, Codes.NotACondition }));
    }

    // Modifiers go on the outside of a function call, never inside it.
    [TestCase("LITERALLY(FALLBACK(genre, \"pop\")) == \"Pop\"")]
    [TestCase("ALL(COUNT(genre)) > 1")]
    [TestCase("ALL(NUMBER(s)) IS USABLE")]
    public void Modifier_OnAFunctionCallOperand_IsValid(string text)
    {
        Compiles(text);
    }

    [TestCase("LITERALLY(year) == 1", "LITERALLY(year)")]
    [TestCase("year == LITERALLY(1)", "LITERALLY(1)")]
    [TestCase("LITERALLY((x < 1)) == TRUE", "LITERALLY((x < 1))")]
    [TestCase("LITERALLY(file::size) BETWEEN 1..2", "LITERALLY(file::size)")]
    public void Literally_OnAnOperandThatIsNotAString_IsAnError(string text, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.LiterallyNotString));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
    }

    [TestCase("LITERALLY(genre) IS USABLE", "genre IS USABLE")]
    [TestCase("LITERALLY(year) IS ABSENT", "year IS ABSENT")]
    [TestCase("ALL(LITERALLY(genre)) IS USABLE", "ALL(genre) IS USABLE")]
    public void Literally_OnTheOperandOfAStateTest_IsAnError(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.LiterallyOnStateTest));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("ALL(genre) IS ABSENT", "genre IS ABSENT")]
    [TestCase("ANY(genre) IS ABSENT", "genre IS ABSENT")]
    [TestCase("(ANY(genre)) IS ABSENT", "(genre) IS ABSENT")]
    public void Quantifier_OnTheOperandOfIsAbsent_IsAnError(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.QuantifierOnAbsentTest));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [TestCase("ALL(genre) IS USABLE", Quantifier.Universal)]
    [TestCase("ANY(genre) IS UNUSABLE", Quantifier.Existential)]
    [TestCase("genre IS USABLE", Quantifier.Existential)]
    public void StateTest_TakesTheQuantifierOfItsOperand(string text, Quantifier quantifier)
    {
        Assert.That(((StateTest<string>)Compiles(text).Root).Quantifier, Is.EqualTo(quantifier));
    }

    [TestCase("ALL(genre) BETWEEN \"a\"..\"b\"", Quantifier.Universal)]
    [TestCase("genre BETWEEN \"a\"..\"b\"", Quantifier.Existential)]
    public void Range_TakesTheQuantifierOfItsSubject(string text, Quantifier quantifier)
    {
        Assert.That(((RangeTest<string>)Compiles(text).Root).Subject.Quantifier, Is.EqualTo(quantifier));
    }

    [Test]
    public void Match_TakesTheQuantifierAndModeOfItsSubject()
    {
        var match = (RegexMatch)Compiles("ALL(LITERALLY(genre)) =~ r\"x\"").Root;

        Assert.That(match.Subject.Quantifier, Is.EqualTo(Quantifier.Universal));
        Assert.That(match.Mode, Is.EqualTo(ComparisonMode.Literal));
    }
}
