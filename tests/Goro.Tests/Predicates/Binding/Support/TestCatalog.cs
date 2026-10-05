using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;

namespace Goro.Tests.Predicates.Binding.Support;

/// <summary>
/// A catalog for binder tests: a closed global namespace of identifiers holding canned bags, as the
/// testing document's <c>x</c>, <c>m</c>, <c>s</c> and <c>t</c> do, with every other namespace
/// answered by the built-in catalog, so that <c>file::size</c> and <c>vorbis::bpm</c> resolve too.
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
    /// the boolean <c>flag</c>; and <c>one</c>, a definite string. All absent.
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
        catalog.Declare("one", isDefinite: true, new CannedBinding<string>([new Usable<string>("one")]));
        return catalog;
    }

    /// <summary>
    /// Declares, or redeclares, an identifier that holds the given bag for every file. A name such as
    /// <c>lab::q</c> declares it in a closed namespace of the test's own.
    /// </summary>
    public TestCatalog With<T>(string name, params Occurrence<T>[] occurrences) where T : notnull =>
        Declare(name, isDefinite: false, new CannedBinding<T>([.. occurrences]));

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

        var sameNamespace = declarations.Keys.Where(declared => declared.Namespace.SequenceEqual(name.Namespace, StringComparer.OrdinalIgnoreCase)).ToList();
        return sameNamespace.Count > 0 || name.IsGlobal
            ? new IdentifierLookup.UnknownIdentifier([.. sameNamespace])
            : BuiltInCatalog.Instance.Lookup(name);
    }

    private TestCatalog Declare<T>(string name, bool isDefinite, CannedBinding<T> binding) where T : notnull
    {
        var parts = name.Split("::");
        var identifier = new IdentifierName(parts[..^1], parts[^1]);
        declarations[identifier] = new IdentifierDeclaration<T>(identifier, isDefinite, binding);
        bindings[name] = binding;
        return this;
    }
}
