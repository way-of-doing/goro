namespace Goro.Predicates.Syntax;

public enum TokenKind
{
    /// <summary>Always the last token, so that the parser never runs off the end.</summary>
    EndOfText,

    // Names and literals; each has a token type of its own carrying what it denotes.
    Name,
    String,
    Number,
    ByteCount,
    Duration,

    // Punctuation.
    OpenParen,
    CloseParen,
    Comma,
    DoubleColon,
    DotDot,

    // Operators.
    EqualEqual,
    BangEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    EqualTilde,

    // Reserved words. The parser still accepts one as a name part after "::".
    And,
    Or,
    Not,
    Between,
    Is,
    Usable,
    Unusable,
    Absent,
    True,
    False,
    Null,
    All,
    Any,
    Literally,

    // Spellings borrowed from other languages. They are tokens rather than lexical errors so that
    // the parser can answer each one in context with what was probably meant.
    Equal,
    LessGreater,
    AmpersandAmpersand,
    BarBar,
    Bang,
    BangTilde,
    TildeEqual,
}
