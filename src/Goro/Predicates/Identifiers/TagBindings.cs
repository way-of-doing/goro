using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The bindings of what reads tags: the cells of the concepts, and the source functions. The tag
/// sources are declared, so a predicate naming one is checked like any other, but reading tags is a
/// line of development of its own (docs/directions/mp3-support.md). Until it lands, every tag read is
/// bound to <see cref="NotImplemented{T}"/>, and that line replaces each use of it with a real binding.
/// </summary>
internal static class TagBindings
{
    public static IdentifierBinding<T> NotImplemented<T>(DeclaredName name) where T : notnull =>
        new NotImplementedBinding<T>(name);

    private sealed class NotImplementedBinding<T>(DeclaredName name) : IdentifierBinding<T> where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin) =>
            throw new NotSupportedException($"Reading tags is not implemented yet, so {name} cannot be read.");
    }
}
