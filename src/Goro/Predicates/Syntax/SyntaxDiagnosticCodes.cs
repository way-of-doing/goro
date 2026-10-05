namespace Goro.Predicates.Syntax;

/// <summary>
/// The codes of the errors the lexer and the parser report. A code names the mistake rather than
/// where it was found, so a few of them can come from either stage.
/// </summary>
public static class SyntaxDiagnosticCodes
{
    // Text that does not divide into tokens.
    public const string UnexpectedCharacter = "unexpected-character";
    public const string NonAsciiName = "non-ascii-name";
    public const string UnterminatedString = "unterminated-string";
    public const string UnknownEscape = "unknown-escape";
    public const string MalformedEscape = "malformed-escape";
    public const string LoneSurrogate = "lone-surrogate";
    public const string SurrogateCodePoint = "surrogate-code-point";
    public const string CodePointOutOfRange = "code-point-out-of-range";
    public const string TrailingPeriod = "trailing-period";
    public const string ClockFieldOutOfRange = "clock-field-out-of-range";
    public const string UnrepresentableLiteral = "unrepresentable-literal";

    // A literal that runs into what follows it, or is split by whitespace.
    public const string MalformedLiteral = "malformed-literal";
    public const string SignedUnitLiteral = "signed-unit-literal";
    public const string WhitespaceInLiteral = "whitespace-in-literal";

    // Tokens in an order the grammar does not allow.
    public const string EmptyPredicate = "empty-predicate";
    public const string UnexpectedEnd = "unexpected-end";
    public const string UnexpectedToken = "unexpected-token";
    public const string ChainedComparison = "chained-comparison";
    public const string BorrowedOperator = "borrowed-operator";
    public const string ReservedWord = "reserved-word";
    public const string ReservedWordInIdentifier = "reserved-word-in-identifier";
    public const string WhitespaceInIdentifier = "whitespace-in-identifier";
    public const string QualifiedFunctionCall = "qualified-function-call";
    public const string RangeEndpointNotLiteral = "range-endpoint-not-literal";
    public const string QuotedPattern = "quoted-pattern";
    public const string ParenthesizedPattern = "parenthesized-pattern";
    public const string ModifiedPattern = "modified-pattern";
    public const string PatternNotRawString = "pattern-not-raw-string";
    public const string IsNot = "is-not";
    public const string IsNull = "is-null";
}
