using System.Collections.Immutable;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;

namespace Goro.Tests.Predicates.Syntax;

/// <summary>Reads predicates as text, the way every test in this folder wants them.</summary>
internal static class SyntaxAssert
{
    public static ImmutableArray<Token> Lexes(string text)
    {
        var result = Lexer.Lex(text);
        Assert.That(result.Succeeded, Is.True, () => $"`{text}` failed to lex: {Describe(result.Diagnostics)}");
        return result.Value;
    }

    /// <summary>The one token a literal lexes to.</summary>
    public static Token SingleToken(string text)
    {
        var tokens = Lexes(text);
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[] { tokens[0].Kind, TokenKind.EndOfText }),
            $"`{text}` is not a single token");
        return tokens[0];
    }

    public static string StringValue(string text) => ((StringToken)SingleToken(text)).Value;

    public static Diagnostic LexError(string text)
    {
        var result = Lexer.Lex(text);
        Assert.That(result.Succeeded, Is.False, $"`{text}` lexed, but should not have");
        Assert.That(result.Diagnostics, Has.Length.EqualTo(1));
        return result.Diagnostics[0];
    }

    public static SyntaxTree Parses(string text)
    {
        var result = SyntaxTree.Parse(text);
        Assert.That(result.Succeeded, Is.True, () => $"`{text}` failed to parse: {Describe(result.Diagnostics)}");
        return result.Value;
    }

    /// <summary>The tree of a predicate, as an S-expression.</summary>
    public static string Tree(string text) => SyntaxPrinter.Print(Parses(text).Root);

    /// <summary>The one error reading a predicate reports, whichever stage finds it.</summary>
    public static Diagnostic Error(string text)
    {
        var result = SyntaxTree.Parse(text);
        Assert.That(result.Succeeded, Is.False, () => $"`{text}` parsed, as {SyntaxPrinter.Print(result.Value.Root)}");
        Assert.That(result.Diagnostics, Has.Length.EqualTo(1));
        return result.Diagnostics[0];
    }

    /// <summary>The predicate each suggestion of a diagnostic would turn the text into.</summary>
    public static string[] Rewrites(string text, Diagnostic diagnostic) =>
        [.. diagnostic.Suggestions.Select(s => s.ApplyTo(text))];

    private static string Describe(ImmutableArray<Diagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(d => $"{d.Code} at {d.Span}: {d.Message}"));
}
