using Goro.Predicates;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Reading.Bytes;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;
using static Goro.Tests.TestSupport.Id3v2Bytes;
using static Goro.Tests.TestSupport.TagBytes;

namespace Goro.Tests.Predicates.Identifiers;

/// <summary>
/// "Concepts written without a source" in docs/testing.md, on MP3s carrying several tag formats:
/// with no Vorbis comments in an MP3, the order of preference is APE, then Id3v2, then Id3v1.
/// </summary>
public class PlainConceptTests
{
    private static Value<decimal> Year(MemoryFileDataLoader file) => TagConceptTests.Resolve<decimal>("year", file);

    private static object[] Data(Value<decimal> value) => TagConceptTests.Data(value);

    private static byte[] Id3v2Year(string year) => Tag(4, [new Frame("TDRC", Latin1Text(year))]);

    [Test]
    public void APreferredFormatAbsent_ALesserOneUsable_GivesTheLesser()
    {
        Assert.That(Data(Year(MemoryFileDataLoader.Mp3(Id3v2Year("1991"), Id3v1(year: "1980")))), Is.EqualTo(new object[] { 1991m }));
    }

    [Test]
    public void APreferredFormatHoldingOnlyUnreadableData_GivesTheLesser()
    {
        var file = MemoryFileDataLoader.Mp3(Id3v2Year("1991"), Ape(ApeText("Year", "last tuesday")));

        Assert.That(Data(Year(file)), Is.EqualTo(new object[] { 1991m }));
    }

    // Usability breaks ties; it does not outrank preference.
    [Test]
    public void APreferredFormatHoldingOneUsableAndOneUnreadable_WinsEntire()
    {
        var file = MemoryFileDataLoader.Mp3(Id3v2Year("1991"), Ape(ApeText("Year", "1980", "junk")));

        Assert.That(Data(Year(file)), Is.EqualTo(new object[] { 1980m, "unusable" }));
    }

    [Test]
    public void OnlyOneFormatHoldingAnything_AndItUnreadable_IsUnusable_NotAbsent()
    {
        Assert.That(Data(Year(MemoryFileDataLoader.Mp3([], Ape(ApeText("Year", "junk"))))), Is.EqualTo(new[] { "unusable" }));
    }

    [Test]
    public void NoFormatHoldingAnything_IsAbsent()
    {
        Assert.That(Year(MemoryFileDataLoader.Mp3([])).IsAbsent, Is.True);
    }

    [Test]
    public void BothUsable_TheLesserIsNotConsulted()
    {
        var file = MemoryFileDataLoader.Mp3(Id3v2Year("1980"), Ape(ApeText("Year", "1991")));
        var lesser = ((StoredBytes.InFile)file.Layout.Source(TagFormat.Id3v2)!.Fields.Single().Stored).Region;

        Assert.That(Data(Year(file)), Is.EqualTo(new object[] { 1991m }));
        Assert.That(file.Reads.Records.Where(r => r.Purpose == ReadPurpose.Payload).Select(r => r.Offset), Has.None.EqualTo(lesser.Start),
            "the Id3v2 year was never read");
    }

    // The documented price of the facade: the junk routed around is not part of the value, so it does not warn.
    [Test]
    public void APreferredFormatsDefectRoutedAround_EmitsNoWarning()
    {
        var file = MemoryFileDataLoader.Mp3(Id3v2Year("1991"), Ape(ApeText("Year", "junk")));
        var compiled = PredicateCompiler.Compile("year == 1991", BuiltInCatalog.Instance);
        var context = new EvaluationContext(new FileData("test.mp3", file), compiled.Value!.Sources.Count);

        Assert.That(compiled.Value.Evaluate(context), Is.EqualTo(Truth.True));
        Assert.That(context.Reported, Is.Empty);
    }

    [Test]
    public void ArtistAndGenre_ComeFromTheMostPreferredFormatHoldingThem()
    {
        var file = MemoryFileDataLoader.Mp3(
            Tag(3, [new Frame("TPE1", Latin1Text("Id3v2 Artist")), new Frame("TCON", Latin1Text("(17)"))]),
            Id3v1(artist: "Id3v1 Artist", genre: 9));

        Assert.That(TagConceptTests.Data(TagConceptTests.Resolve<string>("artist", file)), Is.EqualTo(new[] { "Id3v2 Artist" }));
        Assert.That(TagConceptTests.Data(TagConceptTests.Resolve<string>("genre", file)), Is.EqualTo(new[] { "Rock" }));
        Assert.That(TagConceptTests.Data(TagConceptTests.Resolve<string>("album", file)), Is.Empty, "no format records an album");
    }
}
