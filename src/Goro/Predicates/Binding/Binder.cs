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
/// <remarks>
/// The work is done by <see cref="Analysis"/>, one per tree. The pieces it uses each have a file of
/// their own in the Binder folder: the error type and canonical shapes (<see cref="Bound"/>), the
/// source interner (<see cref="Sources"/>), the bridge into the typed nodes (<see cref="TypedNodes"/>),
/// pattern compilation (<see cref="Patterns"/>), and the diagnostics with their suggested rewrites
/// (<see cref="BinderDiagnostics"/>).
/// </remarks>
public static class Binder
{
    public static StageResult<CompiledPredicate> Bind(SyntaxTree tree, IIdentifierCatalog catalog) => Analysis.Run(tree, catalog);
}
