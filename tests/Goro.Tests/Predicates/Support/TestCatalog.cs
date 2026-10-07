using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;

namespace Goro.Tests.Predicates.Support;

/// <summary>
/// A catalog for binder tests: concepts written without a source, holding canned bags, as the
/// testing document's <c>x</c>, <c>m</c>, <c>s</c> and <c>t</c> do, with every real source answered
/// by the built-in catalog, so that <c>file::size</c> and <c>vorbis::field("BPM")</c> resolve too.
/// </summary>
/// <remarks>
/// <see cref="Standard"/> declares a fixed set of names, every one absent, which is all a test of
/// static analysis needs. A test that evaluates gives the names it reads their bags with
/// <see cref="With{T}(string, Occurrence{T}[])"/>.
/// </remarks>
internal sealed class TestCatalog : IIdentifierCatalog
{
    private readonly Dictionary<IdentifierName, IdentifierDeclaration> declarations = [];
    private readonly Dictionary<string, object> bindings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Numbers <c>a</c>, <c>b</c>, <c>c</c>, <c>d</c>, <c>v</c>, <c>w</c>, <c>x</c>, <c>y</c> and
    /// <c>year</c>; strings <c>artist</c>, <c>genre</c>, <c>m</c>, <c>s</c>, <c>t</c> and <c>title</c>;
    /// the boolean <c>flag</c>; and <c>one</c>, a string that is exactly one. All absent.
    /// </summary>
    public static TestCatalog Standard()
    {
        var catalog = new TestCatalog();
        foreach (var name in new[] { "a", "b", "c", "d", "v", "w", "x", "y", "year" })
        {
            catalog.With<decimal>(name);
        }

        foreach (var name in new[] { "artist", "genre", "m", "s", "t", "title" })
        {
            catalog.With<string>(name);
        }

        catalog.With<bool>("flag");
        catalog.Declare("one", Bounds.ExactlyOne, new CannedBinding<string>([new Usable<string>("one")]));
        return catalog;
    }

    /// <summary>
    /// Declares, or redeclares, an identifier that holds the given bag for every file. A name such as
    /// <c>lab::q</c> declares it in a source of the test's own.
    /// </summary>
    public TestCatalog With<T>(string name, params Occurrence<T>[] occurrences) where T : notnull =>
        Declare(name, Bounds.Any, new CannedBinding<T>([.. occurrences]));

    /// <summary>How often the named identifier was resolved: a test's way of seeing that nothing was evaluated.</summary>
    public int Resolutions(string name) => bindings[name] switch
    {
        CannedBinding<decimal> binding => binding.Resolutions,
        CannedBinding<string> binding => binding.Resolutions,
        CannedBinding<bool> binding => binding.Resolutions,
        var binding => throw new InvalidOperationException($"{binding} is not a canned binding."),
    };

    public IdentifierLookup Lookup(IdentifierName name)
    {
        if (declarations.TryGetValue(name, out var declaration))
        {
            return new IdentifierLookup.Found(declaration);
        }

        // A concept of the file source written without it is answered as the built-in catalog answers it.
        if (!name.IsQualified && BuiltInCatalog.Instance.Lookup(name) is IdentifierLookup.NeedsSource needs)
        {
            return needs;
        }

        var sameSource = declarations.Keys
            .Where(declared => string.Equals(declared.Source, name.Source, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return sameSource.Count > 0 || !name.IsQualified
            ? new IdentifierLookup.UnknownIdentifier([.. sameSource.Select(declared => declared.Name)])
            : BuiltInCatalog.Instance.Lookup(name);
    }

    public SourceFunctionLookup LookupFunction(string source, string function) =>
        BuiltInCatalog.Instance.LookupFunction(source, function);

    private TestCatalog Declare<T>(string name, Bounds bounds, CannedBinding<T> binding) where T : notnull
    {
        var parts = name.Split("::");
        var identifier = parts.Length == 2 ? new IdentifierName(parts[0], parts[1]) : IdentifierName.Plain(name);
        declarations[identifier] = new IdentifierDeclaration<T>(identifier, bounds, binding);
        bindings[name] = binding;
        return this;
    }
}
