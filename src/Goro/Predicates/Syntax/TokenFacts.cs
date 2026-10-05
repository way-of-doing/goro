using System.Collections.Frozen;

namespace Goro.Predicates.Syntax;

/// <summary>What the lexer and the parser both need to know about token kinds.</summary>
internal static class TokenFacts
{
    private static readonly FrozenDictionary<string, TokenKind> ReservedWords =
        new Dictionary<string, TokenKind>
        {
            ["AND"] = TokenKind.And,
            ["OR"] = TokenKind.Or,
            ["NOT"] = TokenKind.Not,
            ["BETWEEN"] = TokenKind.Between,
            ["IS"] = TokenKind.Is,
            ["USABLE"] = TokenKind.Usable,
            ["UNUSABLE"] = TokenKind.Unusable,
            ["ABSENT"] = TokenKind.Absent,
            ["TRUE"] = TokenKind.True,
            ["FALSE"] = TokenKind.False,
            ["NULL"] = TokenKind.Null,
            ["ALL"] = TokenKind.All,
            ["ANY"] = TokenKind.Any,
            ["LITERALLY"] = TokenKind.Literally,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The reserved word a name spells, matched without regard to case.</summary>
    public static bool TryGetReservedWord(string name, out TokenKind kind) => ReservedWords.TryGetValue(name, out kind);

    public static bool IsReservedWord(TokenKind kind) => kind is >= TokenKind.And and <= TokenKind.Literally;

    public static bool IsModifier(TokenKind kind) => kind is TokenKind.All or TokenKind.Any or TokenKind.Literally;

    /// <summary>A token the grammar's <c>literal</c> production accepts.</summary>
    public static bool IsLiteral(TokenKind kind) =>
        kind is TokenKind.String or TokenKind.Number or TokenKind.ByteCount or TokenKind.Duration
            or TokenKind.True or TokenKind.False;

    /// <summary>A number, bytecount or duration: the literals that can run into the text after them.</summary>
    public static bool IsNumeric(TokenKind kind) =>
        kind is TokenKind.Number or TokenKind.ByteCount or TokenKind.Duration;

    /// <summary>A token made of letters and digits, which nothing separates from a word before it.</summary>
    public static bool IsWordLike(TokenKind kind) => kind == TokenKind.Name || IsReservedWord(kind) || IsNumeric(kind);

    public static bool IsBorrowedOperator(TokenKind kind) => kind is >= TokenKind.Equal and <= TokenKind.TildeEqual;

    /// <summary>A token that begins the tail of a comparison, or would if it were not borrowed.</summary>
    public static bool StartsComparisonTail(TokenKind kind) =>
        kind is TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.Less or TokenKind.LessEqual
            or TokenKind.Greater or TokenKind.GreaterEqual or TokenKind.EqualTilde or TokenKind.Between
            or TokenKind.Is or TokenKind.Equal or TokenKind.LessGreater or TokenKind.TildeEqual
            or TokenKind.BangTilde;
}
