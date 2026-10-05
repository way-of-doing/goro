using System.Text.RegularExpressions;
using Goro.Predicates.Binding;
using Goro.Predicates.Text;
using static Goro.Tests.Predicates.Binding.Support.BindAssert;
using Codes = Goro.Predicates.Binding.BinderDiagnosticCodes;

namespace Goro.Tests.Predicates.Binding;

/// <summary>Patterns, compiled when the predicate is read, by the non-backtracking engine.</summary>
public class PatternBindingTests
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
    [TestCase(@"(?=a)", "lookaround")]
    [TestCase(@"(?!a)", "lookaround")]
    [TestCase(@"(?<=a)b", "lookaround")]
    [TestCase(@"(?<!a)b", "lookaround")]
    [TestCase(@"(a)\1", "backreference")]
    [TestCase(@"(?<n>a)\k<n>", "backreference")]
    [TestCase(@"(?>a)", "atomic group")]
    [TestCase(@"(a)?(?(1)b|c)", "conditional or a balancing group")]
    [TestCase(@"(?(a)b|c)", "conditional or a balancing group")]
    [TestCase(@"(?<x>a)(?<-x>b)", "conditional or a balancing group")]
    public void UnsupportedConstruct_IsRejected_NamingItsFamily(string pattern, string family)
    {
        var text = $"artist =~ r\"{pattern}\"";

        var error = Error(text);

        Assert.That(error.Code, Is.EqualTo(Codes.UnsupportedPatternConstruct));
        Assert.That(error.Message, Does.Contain(family));
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
        Assert.That(Error("artist =~ r\"a(\"").Message, Does.Contain("Not enough )'s"));
    }

    [TestCase("year =~ r\"1\"", "STRING(year) =~ r\"1\"")]
    [TestCase("ALL(year) =~ r\"1\"", "ALL(STRING(year)) =~ r\"1\"")]
    [TestCase("file::size =~ r\"1\"", "STRING(file::size) =~ r\"1\"")]
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
