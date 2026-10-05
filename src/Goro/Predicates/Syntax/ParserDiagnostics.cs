using System.Collections.Immutable;
using Goro.Messages;
using Goro.Predicates.Diagnostics;
using Codes = Goro.Predicates.Syntax.SyntaxDiagnosticCodes;

namespace Goro.Predicates.Syntax;

/// <summary>
/// The parser's errors, and the rewrites it offers for them. Every rewrite is composed from the
/// spans of what the user wrote, so that it changes only what was wrong.
/// </summary>
internal sealed class ParserDiagnostics(string text, ImmutableArray<Token> tokens)
{
    private const string LiterallyKeyword = "LITERALLY";

    /// <summary>
    /// The token at <paramref name="index"/> cannot stand where it is. Before falling back to saying
    /// so, this looks for the likelier stories: an operator borrowed from another language, a literal
    /// running straight into the text after it, or one split by a space.
    /// </summary>
    public Diagnostic Unexpected(int index, Expectation expected)
    {
        var token = tokens[index];
        if (TokenFacts.IsBorrowedOperator(token.Kind))
        {
            return BorrowedOperator(token);
        }

        if (index > 0 && tokens[index - 1] is var previous && TokenFacts.IsNumeric(previous.Kind) && TokenFacts.IsWordLike(token.Kind))
        {
            if (previous.Span.End == token.Span.Start)
            {
                return RunOnLiteral(index);
            }

            if (WhitespaceInLiteral(previous, token) is { } split)
            {
                return split;
            }
        }

        return token.Kind == TokenKind.EndOfText
            ? new Diagnostic(Codes.UnexpectedEnd, token.Span, new ErrorMessage.UnexpectedEnd(expected))
            : new Diagnostic(Codes.UnexpectedToken, token.Span, new ErrorMessage.UnexpectedToken(expected, Code(token)));
    }

    public Diagnostic ChainedComparison(ExpressionSyntax comparison, Token second)
    {
        // a == b == c  ->  a == b AND b == c, which is how predicates.md says to write it.
        ImmutableArray<Suggestion> suggestions = comparison is ComparisonSyntax { Right: var middle }
            ? [new Suggestion(new TextSpan(middle.Span.End, 0), $" AND {Text(middle)}")]
            : [];
        return new Diagnostic(Codes.ChainedComparison, second.Span, new ErrorMessage.ChainedComparison(), suggestions);
    }

    /// <summary><c>~=</c> or <c>!~</c> in place of a comparison operator, with the operand after it if one could be read.</summary>
    public Diagnostic BorrowedMatchOperator(ExpressionSyntax subject, Token borrowed, ExpressionSyntax? operand)
    {
        var pattern = (operand as LiteralSyntax)?.Token as StringToken;
        if (borrowed.Kind == TokenKind.TildeEqual)
        {
            var suggestions = ImmutableArray.CreateBuilder<Suggestion>();
            if (pattern is not null)
            {
                suggestions.Add(new Suggestion(TextSpan.Covering(borrowed.Span, pattern.Span),
                    $"=~{Between(borrowed.Span, pattern.Span)}{RawForm(pattern)}"));
            }

            suggestions.Add(new Suggestion(borrowed.Span, "!="));
            return new Diagnostic(Codes.BorrowedOperator, borrowed.Span, new ErrorMessage.BorrowedTildeEqual(), suggestions.ToImmutable());
        }

        ImmutableArray<Suggestion> negated = pattern is null
            ? []
            : [new Suggestion(TextSpan.Covering(subject.Span, pattern.Span),
                $"NOT {Text(TextSpan.FromBounds(subject.Span.Start, borrowed.Span.Start))}=~{Between(borrowed.Span, pattern.Span)}{RawForm(pattern)}")];
        return new Diagnostic(Codes.BorrowedOperator, borrowed.Span, new ErrorMessage.BorrowedBangTilde(), negated);
    }

    /// <summary>What followed <c>=~</c> was read as an operand, and was not a bare raw string.</summary>
    public Diagnostic NotARawPattern(ExpressionSyntax subject, Token match, ExpressionSyntax pattern)
    {
        var core = pattern;
        var parenthesized = false;
        var modifiers = new List<ModifierSyntax>();
        while (true)
        {
            if (core is ParenthesizedSyntax parentheses)
            {
                parenthesized = true;
                core = parentheses.Expression;
            }
            else if (core is ModifierSyntax modifier)
            {
                modifiers.Add(modifier);
                core = modifier.Operand;
            }
            else
            {
                break;
            }
        }

        if (core is not LiteralSyntax { Token: StringToken literal })
        {
            return new Diagnostic(Codes.PatternNotRawString, pattern.Span, new ErrorMessage.PatternNotRawString(Code(match)));
        }

        if (modifiers.Count > 0)
        {
            // LITERALLY sets the operator's mode, so it means the same on the subject; a quantifier has no such reading.
            ImmutableArray<Suggestion> moved = modifiers.TrueForAll(m => m.Modifier == ModifierKind.Literally)
                ? [new Suggestion(TextSpan.Covering(subject.Span, pattern.Span), LiterallyOnSubject(subject, modifiers[0], pattern, literal))]
                : [];
            return new Diagnostic(Codes.ModifiedPattern, pattern.Span, new ErrorMessage.ModifiedPattern(), moved);
        }

        if (parenthesized)
        {
            return new Diagnostic(Codes.ParenthesizedPattern, pattern.Span, new ErrorMessage.ParenthesizedPattern(),
                [new Suggestion(pattern.Span, RawForm(literal))]);
        }

        return new Diagnostic(Codes.QuotedPattern, literal.Span, new ErrorMessage.QuotedPattern(),
            [new Suggestion(literal.Span, RawForm(literal))]);
    }

    public Diagnostic IsNot(ExpressionSyntax operand, Token @is, Token not, Token next)
    {
        ImmutableArray<Suggestion> negated = next.Kind is TokenKind.Usable or TokenKind.Unusable or TokenKind.Absent or TokenKind.Null
            ? [new Suggestion(TextSpan.Covering(operand.Span, next.Span),
                $"NOT ({Text(operand)} {Text(@is)} {(next.Kind == TokenKind.Null ? "ABSENT" : Text(next))})")]
            : [];
        return new Diagnostic(Codes.IsNot, TextSpan.Covering(@is.Span, not.Span), new ErrorMessage.IsNot(), negated);
    }

    public Diagnostic IsNull(Token @null) =>
        new(Codes.IsNull, @null.Span, new ErrorMessage.IsNull(), [new Suggestion(@null.Span, "ABSENT")]);

    public Diagnostic RangeEndpointNotLiteral(int index)
    {
        var token = tokens[index];
        var message = new ErrorMessage.RangeEndpointNotLiteral();
        if (token.Kind == TokenKind.OpenParen && index + 2 < tokens.Length
            && TokenFacts.IsLiteral(tokens[index + 1].Kind) && tokens[index + 2].Kind == TokenKind.CloseParen)
        {
            var span = TextSpan.Covering(token.Span, tokens[index + 2].Span);
            return new Diagnostic(Codes.RangeEndpointNotLiteral, span, message, [new Suggestion(span, Text(tokens[index + 1]))]);
        }

        return new Diagnostic(Codes.RangeEndpointNotLiteral, token.Span, message);
    }

    public Diagnostic ReservedWord(Token word)
    {
        var spelling = Code(word);
        return new Diagnostic(Codes.ReservedWord, word.Span, TokenFacts.IsModifier(word.Kind)
            ? new ErrorMessage.ModifierAsWord(spelling)
            : new ErrorMessage.ReservedWord(spelling));
    }

    public Diagnostic ReservedWordInIdentifier(Token word) =>
        new(Codes.ReservedWordInIdentifier, word.Span, new ErrorMessage.ReservedWordInIdentifier(Code(word)));

    public Diagnostic WhitespaceInIdentifier(IdentifierSyntax identifier, IEnumerable<TextSpan> tokens) =>
        new(Codes.WhitespaceInIdentifier, identifier.Span, new ErrorMessage.WhitespaceInIdentifier(),
            [new Suggestion(identifier.Span, string.Concat(tokens.Select(token => token.Of(text))))]);

    public Diagnostic QualifiedFunctionCall(IdentifierSyntax identifier)
    {
        var name = identifier.Parts[^1];
        ImmutableArray<Suggestion> unqualified = name.IsQuoted ? [] : [new Suggestion(identifier.Span, name.Text)];
        return new Diagnostic(Codes.QualifiedFunctionCall, identifier.Span, new ErrorMessage.QualifiedFunctionCall(Code(identifier)), unqualified);
    }

    // ----------------------------------------------------------------------------------------------

    private Diagnostic BorrowedOperator(Token token)
    {
        (ErrorMessage message, ImmutableArray<Suggestion> suggestions) = token.Kind switch
        {
            TokenKind.Equal => (new ErrorMessage.BorrowedEqual(), [new Suggestion(token.Span, "==")]),
            TokenKind.LessGreater => (new ErrorMessage.BorrowedLessGreater(), [new Suggestion(token.Span, "!=")]),
            TokenKind.AmpersandAmpersand => (new ErrorMessage.BorrowedAnd(), [Word(token.Span, "AND")]),
            TokenKind.BarBar => (new ErrorMessage.BorrowedOr(), [Word(token.Span, "OR")]),
            TokenKind.Bang => (new ErrorMessage.BorrowedBang(), [Word(token.Span, "NOT")]),
            TokenKind.TildeEqual => (new ErrorMessage.BorrowedTildeEqual(), ImmutableArray<Suggestion>.Empty),
            _ => ((ErrorMessage)new ErrorMessage.BorrowedBangTilde(), ImmutableArray<Suggestion>.Empty),
        };
        return new Diagnostic(Codes.BorrowedOperator, token.Span, message, suggestions);
    }

    /// <summary>A keyword in place of a symbol, with a space added where the symbol touched a neighbour.</summary>
    private Suggestion Word(TextSpan span, string word)
    {
        var before = span.Start > 0 && !char.IsWhiteSpace(text[span.Start - 1]) && text[span.Start - 1] != '(' ? " " : "";
        var after = span.End < text.Length && !char.IsWhiteSpace(text[span.End]) && text[span.End] != ')' ? " " : "";
        return new Suggestion(span, before + word + after);
    }

    /// <summary>A number, bytecount or duration that the token at <paramref name="index"/> runs straight into.</summary>
    private Diagnostic RunOnLiteral(int index)
    {
        var literal = tokens[index - 1];
        var next = tokens[index];
        var last = index;
        while (TokenFacts.IsWordLike(tokens[last + 1].Kind) && tokens[last + 1].Span.Start == tokens[last].Span.End)
        {
            last++;
        }

        var span = TextSpan.Covering(literal.Span, tokens[last].Span);
        var run = Text(span);
        var written = Text(literal);

        if (literal.Kind == TokenKind.Number && written[0] is '+' or '-'
            && Lexer.Lex(run[1..]) is { Succeeded: true, Value: var unsigned } && unsigned[0].Kind is TokenKind.ByteCount or TokenKind.Duration)
        {
            return new Diagnostic(Codes.SignedUnitLiteral, span, new ErrorMessage.SignedUnitLiteral(new Code(run)));
        }

        if (literal.Kind == TokenKind.Number && written.Contains('.') && char.ToLowerInvariant(Text(next)[0]) is 'h' or 'm' or 's')
        {
            return new Diagnostic(Codes.MalformedLiteral, span, new ErrorMessage.FractionalDurationFields(new Code(run)));
        }

        var kind = literal.Kind switch
        {
            TokenKind.ByteCount => NumericLiteral.ByteCount,
            TokenKind.Duration => NumericLiteral.Duration,
            _ => NumericLiteral.Number,
        };
        return new Diagnostic(Codes.MalformedLiteral, span, new ErrorMessage.RunOnLiteral(new Code(run), new Code(written), kind, Code(next)));
    }

    /// <summary><c>10 kb</c> or <c>1h 10m</c>: two tokens that would be one literal without the whitespace between them.</summary>
    private Diagnostic? WhitespaceInLiteral(Token literal, Token next)
    {
        var gap = Text(TextSpan.FromBounds(literal.Span.End, next.Span.Start));
        if (!gap.All(char.IsWhiteSpace))
        {
            return null;
        }

        var joined = Text(literal) + Text(next);
        if (Lexer.Lex(joined) is not { Succeeded: true, Value: [{ Kind: TokenKind.ByteCount or TokenKind.Duration }, { Kind: TokenKind.EndOfText }] })
        {
            return null;
        }

        var span = TextSpan.Covering(literal.Span, next.Span);
        return new Diagnostic(Codes.WhitespaceInLiteral, span, new ErrorMessage.WhitespaceInLiteral(Code(span)), [new Suggestion(span, joined)]);
    }

    private string LiterallyOnSubject(ExpressionSyntax subject, ModifierSyntax literally, ExpressionSyntax pattern, StringToken literal)
    {
        var between = Between(subject.Span, pattern.Span);
        if (HasLiterally(subject))
        {
            return $"{Text(subject)}{between}{RawForm(literal)}";
        }

        var keyword = Text(new TextSpan(literally.Span.Start, LiterallyKeyword.Length));
        return $"{keyword}({Text(subject)}){between}{RawForm(literal)}";
    }

    private static bool HasLiterally(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedSyntax parentheses => HasLiterally(parentheses.Expression),
        ModifierSyntax modifier => modifier.Modifier == ModifierKind.Literally || HasLiterally(modifier.Operand),
        _ => false,
    };

    /// <summary>A string as a raw string: as written if it was one, and otherwise with every quote doubled.</summary>
    private string RawForm(StringToken token) =>
        token.IsRaw ? Text(token) : $"r\"{token.Value.Replace("\"", "\"\"")}\"";

    private string Between(TextSpan first, TextSpan second) => Text(TextSpan.FromBounds(first.End, second.Start));

    private string Text(Token token) => Text(token.Span);

    private string Text(SyntaxNode node) => Text(node.Span);

    private string Text(TextSpan span) => span.Of(text);

    private Code Code(Token token) => new(Text(token));

    private Code Code(SyntaxNode node) => new(Text(node));

    private Code Code(TextSpan span) => new(Text(span));
}
