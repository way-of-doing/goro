// Owned by the syntax group (G2 + G3) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using Goro.Messages;
using Goro.Predicates.Diagnostics;
using static Goro.Predicates.Syntax.SyntaxDiagnosticCodes;

namespace Goro.Predicates.Syntax;

/// <summary>
/// Builds a syntax tree by recursive descent with a single token of lookahead. Stops at the first
/// error.
/// </summary>
/// <remarks>
/// Each grammar rule of predicates.md is one method of <see cref="Reader"/>, named after it. In a
/// few places the parser reads more than the grammar allows, so that a likely mistake can be
/// answered with what to write instead; <see cref="ParserDiagnostics"/> composes those answers.
/// </remarks>
public static class Parser
{
    public static StageResult<SyntaxTree> Parse(string text, ImmutableArray<Token> tokens)
    {
        try
        {
            return StageResult<SyntaxTree>.Success(new SyntaxTree(text, new Reader(text, tokens).Predicate()));
        }
        catch (SyntaxError error)
        {
            return StageResult<SyntaxTree>.Failure(error.Diagnostic);
        }
    }

    // Parsing stops at the first error, so an exception unwinds every rule at once rather than each
    // rule checking what the ones it called returned. It never leaves Parse.
    private sealed class SyntaxError(Diagnostic diagnostic) : Exception(diagnostic.Code)
    {
        public Diagnostic Diagnostic { get; } = diagnostic;
    }

    private sealed class Reader(string text, ImmutableArray<Token> tokens)
    {
        private readonly ParserDiagnostics diagnostics = new(text, tokens);
        private int index;

        private Token Current => tokens[index];

        public ExpressionSyntax Predicate()
        {
            if (Current.Kind == TokenKind.EndOfText)
            {
                throw Error(new Diagnostic(EmptyPredicate, new TextSpan(0, text.Length), new ErrorMessage.EmptyPredicate()));
            }

            var expression = Expression();
            Expect(TokenKind.EndOfText, Expectation.OperatorOrEnd);
            return expression;
        }

        // expression = or_expr ;  or_expr = and_expr { "OR" and_expr } ;
        private ExpressionSyntax Expression()
        {
            var left = AndExpression();
            while (Current.Kind == TokenKind.Or)
            {
                Advance();
                var right = AndExpression();
                left = new LogicalSyntax(TextSpan.Covering(left.Span, right.Span), left, LogicalOperator.Or, right);
            }

            return left;
        }

        // and_expr = not_expr { "AND" not_expr } ;
        private ExpressionSyntax AndExpression()
        {
            var left = NotExpression();
            while (Current.Kind == TokenKind.And)
            {
                Advance();
                var right = NotExpression();
                left = new LogicalSyntax(TextSpan.Covering(left.Span, right.Span), left, LogicalOperator.And, right);
            }

            return left;
        }

        // not_expr = "NOT" not_expr | comparison_expr ;
        private ExpressionSyntax NotExpression()
        {
            if (Current.Kind != TokenKind.Not)
            {
                return ComparisonExpression();
            }

            var not = Advance();
            var operand = NotExpression();
            return new NotSyntax(TextSpan.Covering(not.Span, operand.Span), operand);
        }

        // comparison_expr = operand [ comparison_tail ] ;
        private ExpressionSyntax ComparisonExpression()
        {
            var left = Operand();
            if (ComparisonTail(left) is not { } comparison)
            {
                return left;
            }

            // The tail appears at most once; a second one is a chain, which is worth naming as such.
            if (TokenFacts.StartsComparisonTail(Current.Kind))
            {
                throw Error(diagnostics.ChainedComparison(comparison, Current));
            }

            return comparison;
        }

        // comparison_tail = comparison_op operand | "BETWEEN" range | "=~" pattern | "IS" state ;
        private ExpressionSyntax? ComparisonTail(ExpressionSyntax left)
        {
            switch (Current.Kind)
            {
                case TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.Less or TokenKind.LessEqual
                    or TokenKind.Greater or TokenKind.GreaterEqual:
                    var op = Advance();
                    var right = Operand();
                    return new ComparisonSyntax(TextSpan.Covering(left.Span, right.Span), left, ComparisonOperatorOf(op.Kind), op.Span, right);

                case TokenKind.Between:
                    Advance();
                    var minimum = RangeEndpoint();
                    Expect(TokenKind.DotDot, Expectation.RangeSeparator);
                    var maximum = RangeEndpoint();
                    return new BetweenSyntax(TextSpan.Covering(left.Span, maximum.Span), left, minimum, maximum);

                case TokenKind.EqualTilde:
                    var match = Advance();
                    return Pattern(left, match);

                case TokenKind.Is:
                    var @is = Advance();
                    return State(left, @is);

                case TokenKind.TildeEqual or TokenKind.BangTilde:
                    // Read on, so that the answer can be built from what was written on both sides.
                    var borrowed = Advance();
                    throw Error(diagnostics.BorrowedMatchOperator(left, borrowed, TryOperand()));

                default:
                    return null;
            }
        }

        // pattern = raw_string ;  read as an operand, so that what was written instead can be named.
        private MatchSyntax Pattern(ExpressionSyntax subject, Token match)
        {
            var pattern = Operand();
            if (pattern is LiteralSyntax { Token: StringToken { IsRaw: true } raw })
            {
                return new MatchSyntax(TextSpan.Covering(subject.Span, raw.Span), subject, raw);
            }

            throw Error(diagnostics.NotARawPattern(subject, match, pattern));
        }

        // state = "USABLE" | "UNUSABLE" | "ABSENT" ;
        private StateTestSyntax State(ExpressionSyntax operand, Token @is)
        {
            TestedState? state = Current.Kind switch
            {
                TokenKind.Usable => TestedState.Usable,
                TokenKind.Unusable => TestedState.Unusable,
                TokenKind.Absent => TestedState.Absent,
                _ => null,
            };

            if (state is { } tested)
            {
                var name = Advance();
                return new StateTestSyntax(TextSpan.Covering(operand.Span, name.Span), operand, tested);
            }

            throw Current.Kind switch
            {
                TokenKind.Not => Error(diagnostics.IsNot(operand, @is, Advance(), Current)),
                TokenKind.Null => Error(diagnostics.IsNull(Current)),
                _ => Error(diagnostics.Unexpected(index, Expectation.State)),
            };
        }

        // range = literal ".." literal ;  the grammar places the literals, so parentheses are not admitted.
        private LiteralSyntax RangeEndpoint()
        {
            if (TokenFacts.IsLiteral(Current.Kind))
            {
                return new LiteralSyntax(Advance());
            }

            var startsOperand = Current.Kind is TokenKind.OpenParen or TokenKind.Name or TokenKind.DoubleColon or TokenKind.Null
                || TokenFacts.IsModifier(Current.Kind);
            throw Error(startsOperand
                ? diagnostics.RangeEndpointNotLiteral(index)
                : diagnostics.Unexpected(index, Expectation.Literal));
        }

        // operand = conversion ;  conversion = modified { "AS" target } ;  target = name ;
        private ExpressionSyntax Operand()
        {
            var operand = Modified();
            while (Current.Kind == TokenKind.As)
            {
                Advance();
                if (Current is not NameToken)
                {
                    throw Error(diagnostics.Unexpected(index, Expectation.Target));
                }

                var target = (NameToken)Advance();
                operand = new AsSyntax(TextSpan.Covering(operand.Span, target.Span), operand, target);
            }

            return operand;
        }

        // modified = modifier "(" operand ")" | primary ;
        private ExpressionSyntax Modified()
        {
            if (!TokenFacts.IsModifier(Current.Kind))
            {
                return Primary();
            }

            var modifier = Advance();
            if (Current.Kind != TokenKind.OpenParen)
            {
                throw Error(Current.Kind == TokenKind.DoubleColon
                    ? diagnostics.ReservedWordInIdentifier(modifier)
                    : diagnostics.ReservedWord(modifier));
            }

            Advance();
            var operand = Operand();
            var close = Expect(TokenKind.CloseParen, Expectation.CloseParen);
            return new ModifierSyntax(TextSpan.Covering(modifier.Span, close.Span), ModifierKindOf(modifier.Kind), operand);
        }

        /// <summary>An operand, or null where none could be read.</summary>
        private ExpressionSyntax? TryOperand()
        {
            try
            {
                return Operand();
            }
            catch (SyntaxError)
            {
                return null;
            }
        }

        // primary = literal | identifier | function_call | "(" expression ")" ;
        private ExpressionSyntax Primary()
        {
            switch (Current.Kind)
            {
                case TokenKind.Name or TokenKind.DoubleColon:
                    return IdentifierOrCall();

                case TokenKind.OpenParen:
                    var open = Advance();
                    var expression = Expression();
                    var close = Expect(TokenKind.CloseParen, Expectation.CloseParen);
                    return new ParenthesizedSyntax(TextSpan.Covering(open.Span, close.Span), expression);

                case var kind when TokenFacts.IsLiteral(kind) || kind == TokenKind.Null:
                    var literal = Advance();
                    RejectReservedWordBeginningIdentifier(literal);
                    return kind == TokenKind.Null ? new NullSyntax(literal.Span) : new LiteralSyntax(literal);

                case var kind when TokenFacts.IsReservedWord(kind) && kind != TokenKind.Not:
                    var word = Advance();
                    RejectReservedWordBeginningIdentifier(word);
                    throw Error(diagnostics.ReservedWord(word));

                default:
                    throw Error(diagnostics.Unexpected(index, Expectation.Operand));
            }
        }

        private void RejectReservedWordBeginningIdentifier(Token token)
        {
            if (TokenFacts.IsReservedWord(token.Kind) && Current.Kind == TokenKind.DoubleColon)
            {
                throw Error(diagnostics.ReservedWordInIdentifier(token));
            }
        }

        // identifier = [ name "::" ] name ;  function_call = [ name "::" ] name "(" [ expression { "," expression } ] ")" ;
        private ExpressionSyntax IdentifierOrCall()
        {
            if (Current.Kind == TokenKind.DoubleColon)
            {
                throw Error(diagnostics.RootedIdentifier(index));
            }

            var first = (NameToken)Advance();
            NameToken? source = null;
            var name = first;
            if (Current.Kind == TokenKind.DoubleColon)
            {
                var colons = Advance();
                name = NameAfterSource(first);
                source = first;

                // An identifier is three tokens, but it is written as one: no whitespace on either side of its "::".
                TextSpan[] tokens = [first.Span, colons.Span, name.Span];
                if (tokens.Zip(tokens.Skip(1)).Any(pair => pair.First.End != pair.Second.Start))
                {
                    throw Error(diagnostics.WhitespaceInIdentifier(TextSpan.Covering(first.Span, name.Span), tokens));
                }

                if (Current.Kind == TokenKind.DoubleColon)
                {
                    throw Error(diagnostics.NestedIdentifier(first, index));
                }
            }

            if (Current.Kind != TokenKind.OpenParen)
            {
                return new IdentifierSyntax(TextSpan.Covering(first.Span, name.Span), source, name);
            }

            return FunctionCall(first, source, name);
        }

        /// <summary>
        /// What follows a source and its "::": a name, which a reserved word never is. A string there is
        /// a name the source records under, which is a source function's to read.
        /// </summary>
        private NameToken NameAfterSource(NameToken source)
        {
            if (Current is NameToken name)
            {
                Advance();
                return name;
            }

            if (TokenFacts.IsReservedWord(Current.Kind))
            {
                throw Error(diagnostics.ReservedWordInIdentifier(Current));
            }

            if (Current is StringToken quoted)
            {
                throw Error(diagnostics.QuotedName(source, quoted));
            }

            throw Error(diagnostics.Unexpected(index, Expectation.Name));
        }

        private FunctionCallSyntax FunctionCall(NameToken first, NameToken? source, NameToken name)
        {
            Advance();
            var arguments = ImmutableArray.CreateBuilder<ExpressionSyntax>();
            if (Current.Kind != TokenKind.CloseParen)
            {
                arguments.Add(Expression());
                while (Current.Kind == TokenKind.Comma)
                {
                    Advance();
                    arguments.Add(Expression());
                }
            }

            var close = Expect(TokenKind.CloseParen, arguments.Count == 0 ? Expectation.CloseParen : Expectation.CommaOrCloseParen);
            return new FunctionCallSyntax(TextSpan.Covering(first.Span, close.Span), source, name, arguments.ToImmutable());
        }

        // ------------------------------------------------------------------------------------------

        private Token Advance()
        {
            var token = Current;
            if (token.Kind != TokenKind.EndOfText)
            {
                index++;
            }

            return token;
        }

        private Token Expect(TokenKind kind, Expectation expected) =>
            Current.Kind == kind ? Advance() : throw Error(diagnostics.Unexpected(index, expected));

        private static SyntaxError Error(Diagnostic diagnostic) => new(diagnostic);

        private static ComparisonOperator ComparisonOperatorOf(TokenKind kind) => kind switch
        {
            TokenKind.EqualEqual => ComparisonOperator.Equal,
            TokenKind.BangEqual => ComparisonOperator.NotEqual,
            TokenKind.Less => ComparisonOperator.Less,
            TokenKind.LessEqual => ComparisonOperator.LessOrEqual,
            TokenKind.Greater => ComparisonOperator.Greater,
            TokenKind.GreaterEqual => ComparisonOperator.GreaterOrEqual,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        private static ModifierKind ModifierKindOf(TokenKind kind) => kind switch
        {
            TokenKind.All => ModifierKind.All,
            TokenKind.Any => ModifierKind.Any,
            TokenKind.Literally => ModifierKind.Literally,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
