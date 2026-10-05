using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The bindings of the identifiers that read tags. The tag namespaces are declared, so a predicate
/// naming one is checked like any other, but reading tags is a line of development of its own
/// (decision D4 of docs/design/predicate-runtime.md). Until it lands, every tag identifier is bound
/// to <see cref="NotImplemented{T}"/>, and that line replaces each use of it in the catalog's tables
/// with a real binding.
/// </summary>
internal static class TagBindings
{
    public static IdentifierBinding<T> NotImplemented<T>(IdentifierName name) where T : notnull =>
        new NotImplementedBinding<T>(name);

    private sealed class NotImplementedBinding<T>(IdentifierName name) : IdentifierBinding<T> where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin) =>
            throw new NotSupportedException($"Tag identifiers are not implemented yet, so {name} cannot be read.");
    }
}
