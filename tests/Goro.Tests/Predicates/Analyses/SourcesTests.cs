using Goro.Predicates.Analyses;
using Goro.Tests.Predicates.Support;

namespace Goro.Tests.Predicates.Analyses;

/// <summary>
/// Warning sources over the semantic tree: the origin each source node is given, and the table.
/// Which spellings make one source is tested end to end in <see cref="Compiler.SourceTests"/>.
/// </summary>
public class SourcesTests
{
    [Test]
    public void SpellingsOfOneSource_ShareItsId_AndKeepTheirOwnText()
    {
        var tree = Analyse.Bind("NUMBER(s) > 1 OR NUMBER( S ) < 5");

        var sources = Sources.Analyse(tree);
        var first = sources.OriginOf(tree.Find("NUMBER(s)"));
        var second = sources.OriginOf(tree.Find("NUMBER( S )"));

        Assert.That(second.Source, Is.EqualTo(first.Source));
        Assert.That((first.Text, first.Start), Is.EqualTo(("NUMBER(s)", 0)));
        Assert.That((second.Text, second.Start), Is.EqualTo(("NUMBER( S )", 17)));
        Assert.That(sources.Table.CanonicalForms, Is.EqualTo(new[] { "s", "NUMBER(s)" }));
    }

    [Test]
    public void ConversionToItsOwnType_IsNoSource()
    {
        Assert.That(Sources.Analyse(Analyse.Bind("NUMBER(x) > 1")).Table.CanonicalForms, Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void NumberStandingForAUnit_HasTheShapeOfThatUnit()
    {
        var tree = Analyse.Bind("NUMBER(FALLBACK(file::size, 1000)) > 1 OR NUMBER(FALLBACK(file::size, 1kb)) > 1");

        Assert.That(Sources.Analyse(tree).Table.CanonicalForms, Is.EqualTo(new[]
        {
            "file::size", "NUMBER(FALLBACK(file::size, 1000b))",
        }));
    }
}
