// Owned by the syntax group (G2 + G3) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using Goro.Predicates.Diagnostics;

namespace Goro.Predicates.Syntax;

/// <summary>
/// Builds a syntax tree by recursive descent with a single token of lookahead. Stops at the first
/// error.
/// </summary>
public static class Parser
{
    public static StageResult<SyntaxTree> Parse(string text, ImmutableArray<Token> tokens) =>
        throw new NotImplementedException();
}
