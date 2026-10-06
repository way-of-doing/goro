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
        var tree = Analyse.Bind("s AS NUMBER > 1 OR S  as  number < 5");

        var sources = Sources.Analyse(tree);
        var first = sources.OriginOf(tree.Find("s AS NUMBER"));
        var second = sources.OriginOf(tree.Find("S  as  number"));

        Assert.That(second.Source, Is.EqualTo(first.Source));
        Assert.That((first.Text, first.Start), Is.EqualTo(("s AS NUMBER", 0)));
        Assert.That((second.Text, second.Start), Is.EqualTo(("S  as  number", 19)));
        Assert.That(sources.Table.CanonicalForms, Is.EqualTo(new[] { "s", "s AS NUMBER" }));
    }

    [Test]
    public void ConversionToItsOwnType_IsNoSource()
    {
        Assert.That(Sources.Analyse(Analyse.Bind("x AS NUMBER > 1")).Table.CanonicalForms, Is.EqualTo(new[] { "x" }));
    }

    // Worked out when the predicate is read, a conversion of a constant can never be unusable.
    [Test]
    public void ConversionOfAConstant_IsNoSource()
    {
        Assert.That(Sources.Analyse(Analyse.Bind("\"5\" AS NUMBER > x")).Table.CanonicalForms, Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void NumberStandingForAUnit_HasTheShapeOfThatUnit()
    {
        var tree = Analyse.Bind("FALLBACK(file::size, 1000) AS NUMBER > 1 OR FALLBACK(file::size, 1kb) AS NUMBER > 1");

        Assert.That(Sources.Analyse(tree).Table.CanonicalForms, Is.EqualTo(new[]
        {
            "file::size", "FALLBACK(file::size, 1000b) AS NUMBER",
        }));
    }
}
