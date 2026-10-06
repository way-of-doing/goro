using System.Text.RegularExpressions;
using Goro.Messages;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Text;
using static Goro.Tests.Predicates.Support.CompileAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>Patterns, compiled when the predicate is read, by the non-backtracking engine.</summary>
public class PatternTests
{
    [Test]
    public void RawPattern_ReachesTheEngineAsWritten()
    {
        var match = (RegexMatch)Compiles(@"artist =~ r""\d{4}""").Root;

        Assert.That(match.Pattern.ToString(), Is.EqualTo(@"\d{4}"));
        Assert.That(match.Pattern.IsMatch("1991"), Is.True);
        Assert.That(match.Pattern.IsMatch("d{4}"), Is.False);
    }

    [Test]
    public void RawPattern_WithADoubledQuote_ReachesTheEngineAsOneQuote()
    {
        var match = (RegexMatch)Compiles(@"artist =~ r""say """"hi""""""").Root;

        Assert.That(match.Pattern.ToString(), Is.EqualTo("say \"hi\""));
    }

    [TestCase("artist =~ r\"a\"", ComparisonMode.Normalized, true)]
    [TestCase("LITERALLY(artist) =~ r\"a\"", ComparisonMode.Literal, false)]
    public void Pattern_IsNonBacktrackingAndInvariant_AndIgnoresCaseOnlyWhenNormalized(string text, ComparisonMode mode, bool ignoresCase)
    {
        var match = (RegexMatch)Compiles(text).Root;

        Assert.That(match.Mode, Is.EqualTo(mode));
        Assert.That(match.Pattern.Options.HasFlag(RegexOptions.NonBacktracking), Is.True);
        Assert.That(match.Pattern.Options.HasFlag(RegexOptions.CultureInvariant), Is.True);
        Assert.That(match.Pattern.Options.HasFlag(RegexOptions.IgnoreCase), Is.EqualTo(ignoresCase));
    }

    // Canaries: each family the engine rejects today. If a later .NET supports one, the
    // specification's promise that it is rejected lapses, and one of these fails.
    [TestCase(@"(?=a)", PatternFamily.Lookaround)]
    [TestCase(@"(?!a)", PatternFamily.Lookaround)]
    [TestCase(@"(?<=a)b", PatternFamily.Lookaround)]
    [TestCase(@"(?<!a)b", PatternFamily.Lookaround)]
    [TestCase(@"(a)\1", PatternFamily.Backreference)]
    [TestCase(@"(?<n>a)\k<n>", PatternFamily.Backreference)]
    [TestCase(@"(?>a)", PatternFamily.AtomicGroup)]
    [TestCase(@"(a)?(?(1)b|c)", PatternFamily.ConditionalOrBalancingGroup)]
    [TestCase(@"(?(a)b|c)", PatternFamily.ConditionalOrBalancingGroup)]
    [TestCase(@"(?<x>a)(?<-x>b)", PatternFamily.ConditionalOrBalancingGroup)]
    public void UnsupportedConstruct_IsRejected_NamingItsFamily(string pattern, PatternFamily family)
    {
        var text = $"artist =~ r\"{pattern}\"";

        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.UnsupportedPatternConstruct));
        Assert.That(error.Message, Is.EqualTo(new ErrorMessage.UnsupportedPatternConstruct(family)));
        Assert.That(Marked(text, error), Is.EqualTo($"r\"{pattern}\""));
    }

    [TestCase("artist =~ r\"a(\"", "(")]
    [TestCase("artist =~ r\"*a\"", "*")]
    [TestCase("artist =~ r\"a{2,1}\"", "}")]
    [TestCase("artist =~ r\"\\p{Nope}\"", "}")]
    [TestCase("artist =~ r\"\"\"(\"", "(")]
    [TestCase("artist =~ r\"(?<-x>a)\"", "x")]
    public void MalformedPattern_IsRejected_AtWhereTheEngineGaveUp(string text, string marked)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.InvalidPattern));
        Assert.That(Marked(text, error), Is.EqualTo(marked));
    }

    [Test]
    public void MalformedPattern_SaysWhy()
    {
        var message = Error("artist =~ r\"a(\"").Message;

        Assert.That(message, Is.TypeOf<ErrorMessage.InvalidPattern>());
        Assert.That(((ErrorMessage.InvalidPattern)message).Pattern, Is.EqualTo(new Code("r\"a(\"")));
        Assert.That(((ErrorMessage.InvalidPattern)message).Reason.Text, Does.Contain("Not enough )'s"));
    }

    [TestCase("year =~ r\"1\"", "year AS STRING =~ r\"1\"")]
    [TestCase("ALL(year) =~ r\"1\"", "ALL(year AS STRING) =~ r\"1\"")]
    [TestCase("file::size =~ r\"1\"", "file::size AS STRING =~ r\"1\"")]
    public void Subject_ThatIsNotAString_IsAnError(string text, string rewrite)
    {
        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.MatchSubjectNotString));
        Assert.That(Rewrites(text, error), Is.EqualTo(new[] { rewrite }));
    }

    [Test]
    public void BadSubjectAndBadPattern_AreTwoErrors()
    {
        Assert.That(Codes("artst =~ r\"(\" OR year =~ r\"(?=a)\""), Is.EqualTo(new[]
        {
            Codes.UnknownIdentifier, Codes.InvalidPattern, Codes.MatchSubjectNotString, Codes.UnsupportedPatternConstruct,
        }));
    }
}
