using Goro.Predicates.Diagnostics;

namespace Goro.Predicates.Syntax;

/// <summary>A parsed predicate, together with the text every span in it refers to.</summary>
public sealed record SyntaxTree(string Text, ExpressionSyntax Root)
{
    /// <summary>Reads a predicate: lexes it, then parses the tokens.</summary>
    public static StageResult<SyntaxTree> Parse(string text) =>
        Lexer.Lex(text).Then(tokens => Parser.Parse(text, tokens));
}
