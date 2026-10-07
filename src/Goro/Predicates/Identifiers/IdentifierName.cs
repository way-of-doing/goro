namespace Goro.Predicates.Identifiers;

/// <summary>
/// The name of an identifier: a concept, and the source it is read from if one was written.
/// Both parts are compared without regard to case, so <c>id3v2::artist</c> and <c>ID3V2::Artist</c>
/// are one name, while <c>artist</c> and <c>id3v2::artist</c> are two, reading different things.
/// </summary>
public sealed class IdentifierName(string? source, string name) : DeclaredName
{
    /// <summary>A concept written without a source.</summary>
    public static IdentifierName Plain(string name) => new(null, name);

    /// <summary>The source written before the concept, or null where none was.</summary>
    public string? Source { get; } = source;

    public string Name { get; } = name;

    public bool IsQualified => Source is not null;

    public override bool Equals(DeclaredName? other) =>
        other is IdentifierName name && SameName(Source, name.Source) && SameName(Name, name.Name);

    public override int GetHashCode() => HashCode.Combine(
        Source is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Source),
        StringComparer.OrdinalIgnoreCase.GetHashCode(Name));

    public override string ToString() => Source is null ? Name : $"{Source}::{Name}";
}
