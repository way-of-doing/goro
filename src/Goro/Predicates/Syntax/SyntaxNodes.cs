using System.Collections.Immutable;
using Goro.Predicates.Diagnostics;

namespace Goro.Predicates.Syntax;

/// <summary>
/// The predicate as written. Nothing here is resolved or checked beyond what the grammar says;
/// parentheses and modifiers are kept where they were written, and every node keeps its span.
/// </summary>
public abstract record SyntaxNode(TextSpan Span);

public abstract record ExpressionSyntax(TextSpan Span) : SyntaxNode(Span);

/// <summary>A string, number, bytecount, duration or boolean literal.</summary>
public sealed record LiteralSyntax(Token Token) : ExpressionSyntax(Token.Span);

/// <summary>
/// <c>NULL</c>, which names nothing. It parses so that static analysis can say what to write instead.
/// </summary>
public sealed record NullSyntax(TextSpan Span) : ExpressionSyntax(Span);

/// <summary>An identifier: the name of a concept, and the source it is read from where one is written.</summary>
public sealed record IdentifierSyntax(TextSpan Span, NameToken? Source, NameToken Name) : ExpressionSyntax(Span);

/// <summary>
/// A function call. A call written with a source, as in <c>vorbis::field("MOOD")</c>, calls a
/// source function of that source; one written without calls a function.
/// </summary>
public sealed record FunctionCallSyntax(TextSpan Span, NameToken? Source, NameToken Name, ImmutableArray<ExpressionSyntax> Arguments)
    : ExpressionSyntax(Span);

/// <summary>
/// <c>operand AS target</c>. The target is any name: which names are targets is the binder's to
/// say, so that a mistyped one can be answered with the closest.
/// </summary>
public sealed record AsSyntax(TextSpan Span, ExpressionSyntax Operand, NameToken Target) : ExpressionSyntax(Span);

public sealed record ParenthesizedSyntax(TextSpan Span, ExpressionSyntax Expression) : ExpressionSyntax(Span);

public sealed record ModifierSyntax(TextSpan Span, ModifierKind Modifier, ExpressionSyntax Operand)
    : ExpressionSyntax(Span);

public sealed record ComparisonSyntax(
    TextSpan Span,
    ExpressionSyntax Left,
    ComparisonOperator Operator,
    TextSpan OperatorSpan,
    ExpressionSyntax Right) : ExpressionSyntax(Span);

/// <summary><c>subject BETWEEN min..max</c>; the endpoints are literals because the grammar says so.</summary>
public sealed record BetweenSyntax(TextSpan Span, ExpressionSyntax Subject, LiteralSyntax Minimum, LiteralSyntax Maximum)
    : ExpressionSyntax(Span);

/// <summary><c>subject =~ pattern</c>; the pattern is always a raw string.</summary>
public sealed record MatchSyntax(TextSpan Span, ExpressionSyntax Subject, StringToken Pattern) : ExpressionSyntax(Span);

public sealed record StateTestSyntax(TextSpan Span, ExpressionSyntax Operand, TestedState State) : ExpressionSyntax(Span);

public sealed record NotSyntax(TextSpan Span, ExpressionSyntax Operand) : ExpressionSyntax(Span);

public sealed record LogicalSyntax(TextSpan Span, ExpressionSyntax Left, LogicalOperator Operator, ExpressionSyntax Right)
    : ExpressionSyntax(Span);
