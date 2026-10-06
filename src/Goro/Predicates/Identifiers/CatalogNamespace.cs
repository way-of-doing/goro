using System.Collections.Immutable;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// One row of a namespace's table, as docs/features/builtins/identifiers.md lists them: a name, a
/// type, its bounds, and where its binding comes from. A row does not yet know its
/// namespace, so that each table can be written as a plain list.
/// </summary>
internal abstract record DeclarationRow(string Name)
{
    public abstract IdentifierDeclaration Declare(ImmutableArray<string> @namespace);
}

/// <param name="Binding">
/// Makes the binding for the identifier, given its full name; a binding that reads tags needs the
/// name to know which field it reads.
/// </param>
internal sealed record DeclarationRow<T>(string Name, Bounds Bounds, Func<IdentifierName, IdentifierBinding<T>> Binding)
    : DeclarationRow(Name) where T : notnull
{
    public override IdentifierDeclaration Declare(ImmutableArray<string> @namespace)
    {
        var name = new IdentifierName(@namespace, Name);
        return new IdentifierDeclaration<T>(name, Bounds, Binding(name));
    }
}

/// <summary>A namespace of the built-in catalog, and how it answers for the names in it.</summary>
internal abstract class CatalogNamespace
{
    private readonly Dictionary<string, IdentifierDeclaration> declarations;

    /// <param name="spelling">The namespace as conventionally written, such as <c>id3v1::raw</c>; empty for the global namespace.</param>
    protected CatalogNamespace(string spelling, IEnumerable<DeclarationRow> rows)
    {
        Spelling = spelling;
        Path = spelling.Length == 0 ? [] : [.. spelling.Split("::")];
        Declarations = [.. rows.Select(row => row.Declare(Path))];
        declarations = Declarations.ToDictionary(declaration => declaration.Name.Name, StringComparer.OrdinalIgnoreCase);
    }

    public string Spelling { get; }

    /// <summary>The namespace's parts, outermost first.</summary>
    public ImmutableArray<string> Path { get; }

    public bool IsGlobal => Path.IsEmpty;

    /// <summary>The identifiers this namespace declares, in the order its table lists them.</summary>
    public ImmutableArray<IdentifierDeclaration> Declarations { get; }

    /// <summary>
    /// Whether <paramref name="name"/> is in this namespace. Parts are compared one by one, as
    /// <see cref="IdentifierName"/> compares them, rather than as joined text: a quoted part may
    /// itself contain <c>::</c>, and <c>ape::"a::b"</c> is not <c>ape::a::b</c>.
    /// </summary>
    public bool Contains(IdentifierName name) => Path.SequenceEqual(name.Namespace, StringComparer.OrdinalIgnoreCase);

    /// <summary>Looks up a name already known to be in this namespace.</summary>
    public abstract IdentifierLookup Lookup(IdentifierName name);

    protected IdentifierDeclaration? Declared(IdentifierName name) => declarations.GetValueOrDefault(name.Name);
}

/// <summary>A namespace whose identifiers are all known in advance; any other name is an error.</summary>
internal sealed class ClosedNamespace(string spelling, IEnumerable<DeclarationRow> rows) : CatalogNamespace(spelling, rows)
{
    public override IdentifierLookup Lookup(IdentifierName name) =>
        Declared(name) is { } declaration
            ? new IdentifierLookup.Found(declaration)
            : new IdentifierLookup.UnknownIdentifier([.. Declarations.Select(candidate => candidate.Name)]);
}

/// <summary>
/// A namespace that accepts every name. Its well-known identifiers are typed as its table lists
/// them; any other name reads the tag field of that name, as a string, which can be absent or repeated.
/// </summary>
/// <param name="field">Makes the binding for a name that is not well known.</param>
internal sealed class OpenNamespace(
    string spelling,
    Func<IdentifierName, IdentifierBinding<string>> field,
    IEnumerable<DeclarationRow> wellKnown) : CatalogNamespace(spelling, wellKnown)
{
    public override IdentifierLookup Lookup(IdentifierName name)
    {
        if (Declared(name) is { } declaration)
        {
            return new IdentifierLookup.Found(declaration);
        }

        // Spelled with the namespace as the table writes it, and the name as the predicate did.
        var fieldName = new IdentifierName(Path, name.Name);
        return new IdentifierLookup.Found(new IdentifierDeclaration<string>(fieldName, Bounds.Any, field(fieldName)));
    }
}
