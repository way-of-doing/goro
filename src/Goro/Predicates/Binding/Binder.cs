// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;

namespace Goro.Predicates.Binding;

/// <summary>
/// Static analysis: resolves every identifier, assigns every sub-expression a type and decides
/// whether it is definite, checks every rule the grammar does not express, interns warning sources
/// and compiles patterns. Reports every independent error it finds.
/// </summary>
public static class Binder
{
    public static StageResult<CompiledPredicate> Bind(SyntaxTree tree, IIdentifierCatalog catalog) =>
        throw new NotImplementedException();
}
