// Owned by the syntax group (G2 + G3) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using Goro.Predicates.Diagnostics;

namespace Goro.Predicates.Syntax;

/// <summary>
/// Divides predicate text into tokens, taking every token as long as it can be and treating
/// whitespace as significant only inside a literal. Stops at the first error.
/// </summary>
public static class Lexer
{
    /// <returns>Every token, ending with <see cref="TokenKind.EndOfText"/>; or the first error.</returns>
    public static StageResult<ImmutableArray<Token>> Lex(string text) => throw new NotImplementedException();
}
