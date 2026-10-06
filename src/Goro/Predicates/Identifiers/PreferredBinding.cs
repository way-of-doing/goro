using System.Collections.Immutable;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The binding of a global identifier: the value <c>PREFERRED()</c> of its <see cref="Candidates"/>
/// would have, chosen by the same <see cref="Preference"/>, so a candidate after the one chosen is
/// never resolved. Each candidate is resolved with the global identifier's own origin, which makes the
/// global identifier the source of every unusable occurrence it holds.
/// </summary>
public sealed class PreferredBinding<T>(ImmutableArray<IdentifierBinding<T>> candidates) : IdentifierBinding<T>
    where T : notnull
{
    /// <summary>The bindings of the format-specific identifiers, most preferred first.</summary>
    public ImmutableArray<IdentifierBinding<T>> Candidates { get; } = candidates;

    public override Value<T> Resolve(FileData file, Origin origin) =>
        Preference.Preferred(Candidates.Select(candidate => candidate.Resolve(file, origin)));
}
