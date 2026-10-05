using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The name of an identifier, independent of how it was written. Parts are compared without regard
/// to case, and a quoted part is just a part, so <c>ape::"Album Artist"</c>,
/// <c>APE::"album artist"</c> and, for a name a bare part can spell, <c>ape::artist</c> against
/// <c>ape::"artist"</c> are each one name. The global namespace is the empty namespace, so
/// <c>artist</c> and <c>::artist</c> are one name as well.
/// </summary>
public sealed partial class IdentifierName : IEquatable<IdentifierName>
{
    public IdentifierName(IEnumerable<string> @namespace, string name)
    {
        Namespace = [.. @namespace];
        Name = name;
    }

    public static IdentifierName Global(string name) => new([], name);

    /// <summary>The namespace parts, outermost first; empty for the global namespace.</summary>
    public ImmutableArray<string> Namespace { get; }

    public string Name { get; }

    public bool IsGlobal => Namespace.IsEmpty;

    public bool Equals(IdentifierName? other) =>
        other is not null
        && Namespace.Length == other.Namespace.Length
        && Namespace.Zip(other.Namespace).All(pair => SameName(pair.First, pair.Second))
        && SameName(Name, other.Name);

    public override bool Equals(object? obj) => Equals(obj as IdentifierName);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var part in Namespace)
        {
            hash.Add(part, StringComparer.OrdinalIgnoreCase);
        }

        hash.Add(Name, StringComparer.OrdinalIgnoreCase);
        return hash.ToHashCode();
    }

    /// <summary>The name as it would be written, quoting any part a bare name cannot spell.</summary>
    public override string ToString() => string.Join("::", Namespace.Append(Name).Select(Spell));

    private static bool SameName(string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

    private static string Spell(string part) =>
        BareName().IsMatch(part) ? part : $"\"{part.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex BareName();
}
