using Goro.Predicates.Values;

namespace Goro.Predicates.Syntax;

/// <summary>
/// One token of a predicate. Punctuation, operators and reserved words are plain tokens; names and
/// literals are the derived records below, which carry what the lexer decoded.
/// </summary>
public record Token(TokenKind Kind, TextSpan Span);

/// <summary>An unquoted name, as written; names are compared without regard to case.</summary>
public sealed record NameToken(TextSpan Span, string Text) : Token(TokenKind.Name, Span);

/// <summary>A quoted or raw string, with its escapes already processed.</summary>
public sealed record StringToken(TextSpan Span, string Value, bool IsRaw) : Token(TokenKind.String, Span);

/// <summary>A number literal, sign included.</summary>
public sealed record NumberToken(TextSpan Span, decimal Value) : Token(TokenKind.Number, Span);

public sealed record ByteCountToken(TextSpan Span, ByteCount Value) : Token(TokenKind.ByteCount, Span);

public sealed record DurationToken(TextSpan Span, Duration Value) : Token(TokenKind.Duration, Span);
