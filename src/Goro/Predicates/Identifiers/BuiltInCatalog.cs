// Owned by the identifier catalog group (G7) of the predicate-runtime-architecture line.
namespace Goro.Predicates.Identifiers;

/// <summary>
/// Every namespace and identifier docs/features/builtins/identifiers.md defines.
/// </summary>
public sealed class BuiltInCatalog : IIdentifierCatalog
{
    public static BuiltInCatalog Instance { get; } = new();

    private BuiltInCatalog()
    {
    }

    public IdentifierLookup Lookup(IdentifierName name) => throw new NotImplementedException();
}
