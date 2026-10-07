using System.Collections.Immutable;

namespace Goro.Predicates.Values;

/// <summary>
/// Data recorded in a file that is not text, such as an attached picture, as <c>bytes()</c> reads it.
/// Nothing in the language reads a blob's content yet: it can be counted and tested for state, and
/// nothing else, so it has no order and no equality of its own.
/// </summary>
public sealed class Blob(ImmutableArray<byte> bytes)
{
    public ImmutableArray<byte> Bytes { get; } = bytes;
}
