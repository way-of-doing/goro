using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Tests.TestSupport;
using static Goro.Tests.TestSupport.Id3v2Bytes;
using static Goro.Tests.TestSupport.TagBytes;

namespace Goro.Tests.Predicates.Identifiers;

/// <summary>
/// The concepts of each tag source on tags built by hand: the first table of docs/testing.md's
/// "Reading audio files", for every Id3v2 revision, and the rules for APE and Id3v1 in
/// docs/features/builtins/identifiers.md.
/// </summary>
public class TagConceptTests
{
    private static readonly Origin Origin = new(new SourceId(0), "concept", 0);

    internal static Value<T> Resolve<T>(string identifier, IFileDataLoader loader) where T : notnull
    {
        var parts = identifier.Split("::");
        var name = parts.Length == 2 ? new IdentifierName(parts[0], parts[1]) : IdentifierName.Plain(identifier);
        var lookup = BuiltInCatalog.Instance.Lookup(name);
        Assert.That(lookup, Is.InstanceOf<IdentifierLookup.Found>(), identifier);
        var declaration = (IdentifierDeclaration<T>)((IdentifierLookup.Found)lookup).Declaration;
        return declaration.Binding.Resolve(new FileData("test.mp3", loader), Origin);
    }

    internal static object[] Data<T>(Value<T> value) where T : notnull =>
        [.. value.Occurrences.Select(o => o is Usable<T>(var datum) ? (object)datum : "unusable")];

    private static MemoryFileDataLoader Id3v2(int major, params (string Frame, byte[] Content)[] frames) =>
        MemoryFileDataLoader.Mp3(Tag(major, frames.Select(f => new Frame(Named(major, f.Frame), f.Content))));

    private static Value<string> Text(string concept, int major, params (string, byte[])[] frames) => Resolve<string>($"id3v2::{concept}", Id3v2(major, frames));

    private static Value<decimal> Number(string concept, int major, params (string, byte[])[] frames) => Resolve<decimal>($"id3v2::{concept}", Id3v2(major, frames));

    private static readonly int[] Revisions = [2, 3, 4];

    // --- Id3v2, every revision ---

    [TestCaseSource(nameof(Revisions))]
    public void AValueWithASlash_IsOneValue(int major)
    {
        Assert.That(Data(Text("artist", major, ("TPE1", Latin1Text("AC/DC")))), Is.EqualTo(new[] { "AC/DC" }));
    }

    [TestCaseSource(nameof(Revisions))]
    public void TrimmingTakesTheEnds_AndNeverTheMiddle(int major)
    {
        Assert.That(Data(Text("artist", major, ("TPE1", Latin1Text(" AC / DC ")))), Is.EqualTo(new[] { "AC / DC" }));
    }

    [TestCaseSource(nameof(Revisions))]
    public void AStringFieldThatIsEmptyOrWhitespace_IsAbsent_ButFieldShowsIt(int major)
    {
        Assert.That(Text("artist", major, ("TPE1", Latin1Text(""))).IsAbsent, Is.True);
        Assert.That(Text("artist", major, ("TPE1", Latin1Text("   "))).IsAbsent, Is.True);
        Assert.That(Data(Field(Id3v2(major, ("TPE1", Latin1Text(""))), "TPE1")), Is.EqualTo(new[] { "" }));
        Assert.That(Data(Field(Id3v2(major, ("TPE1", Latin1Text("   "))), "TPE1")), Is.EqualTo(new[] { "   " }));
    }

    /// <summary><c>id3v2::field(frame)</c>, which trims nothing.</summary>
    private static Value<string> Field(IFileDataLoader file, string frame)
    {
        var function = ((SourceFunctionLookup.Found)BuiltInCatalog.Instance.LookupFunction("id3v2", "field")).Function;
        var declaration = (IdentifierDeclaration<string>)((SourceFunctionResolution.Found)function.Resolve([frame])).Declaration;
        return declaration.Binding.Resolve(new FileData("test.mp3", file), Origin);
    }

    [TestCaseSource(nameof(Revisions))]
    public void ANumberFieldThatIsEmptyOrWhitespace_IsUnusable(int major)
    {
        Assert.That(Data(Number("year", major, ("TDRC", Latin1Text("   ")))), Is.EqualTo(new[] { "unusable" }));
        Assert.That(Data(Number("track", major, ("TRCK", Latin1Text("")))), Is.EqualTo(new[] { "unusable" }));
    }

    [TestCaseSource(nameof(Revisions))]
    public void SeveralValuesInOneFrame_AreSeveralOccurrences(int major)
    {
        Assert.That(Data(Text("artist", major, ("TPE1", Latin1Text("A", "B")))), Is.EqualTo(new[] { "A", "B" }));
    }

    [TestCaseSource(nameof(Revisions))]
    public void TheSameFrameTwice_IsTwoOccurrences(int major)
    {
        Assert.That(Data(Text("title", major, ("TIT2", Latin1Text("one")), ("TIT2", Latin1Text("two")))), Is.EqualTo(new[] { "one", "two" }));
    }

    [TestCase(2, "Rock; Metal", new[] { "Rock", "Metal" })]
    [TestCase(3, "Rock; Metal", new[] { "Rock", "Metal" })]
    [TestCase(4, "Rock; Metal", new[] { "Rock", "Metal" })]
    [TestCase(2, "17", new[] { "Rock" })]
    [TestCase(3, "(17)", new[] { "Rock" })]
    [TestCase(4, "(17)", new[] { "Rock" })]
    [TestCase(2, "(17)Post-Rock", new[] { "Rock", "Post-Rock" })]
    [TestCase(3, "(17)Post-Rock", new[] { "Rock", "Post-Rock" })]
    [TestCase(4, "(17)Post-Rock", new[] { "Rock", "Post-Rock" })]
    [TestCase(3, "(17)Rock", new[] { "Rock" })]
    [TestCase(4, "(51)(39)", new[] { "Techno-Industrial", "Noise" })]
    [TestCase(3, "(17)((weird)", new[] { "Rock", "(weird)" })]
    [TestCase(2, "(RX)", new[] { "Remix" })]
    [TestCase(4, "RX", new[] { "Remix" })]
    [TestCase(3, "CR", new[] { "Cover" })]
    public void AGenre_FollowsTheTconConventions_OnEveryRevision(int major, string recorded, string[] expected)
    {
        Assert.That(Data(Text("genre", major, ("TCON", Latin1Text(recorded)))), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(Revisions))]
    public void SeveralGenreValuesInOneFrame_AreEachResolved(int major)
    {
        Assert.That(Data(Text("genre", major, ("TCON", Latin1Text("17", "Post-Rock")))), Is.EqualTo(new[] { "Rock", "Post-Rock" }));
    }

    [TestCaseSource(nameof(Revisions))]
    public void ATrackOf3Of12_IsTrack3(int major)
    {
        Assert.That(Data(Number("track", major, ("TRCK", Latin1Text("3/12")))), Is.EqualTo(new object[] { 3m }));
    }

    [TestCase(2, "1991")]
    [TestCase(3, "1991-01-02")]
    [TestCase(4, "1991-01-02T12:30:59")]
    public void AYear_IsReadOutOfADate(int major, string recorded)
    {
        Assert.That(Data(Number("year", major, ("TDRC", Latin1Text(recorded)))), Is.EqualTo(new object[] { 1991m }));
    }

    [TestCase(3, "last tuesday")]
    [TestCase(4, "0000")]
    public void AYearThatIsNoDate_IsUnusable(int major, string recorded)
    {
        Assert.That(Data(Number("year", major, ("TDRC", Latin1Text(recorded)))), Is.EqualTo(new[] { "unusable" }));
    }

    // A TYER frame is a TDRC wherever it is found, so it is the year in a v2.4 tag too.
    [TestCase(3)]
    [TestCase(4)]
    public void TyerIsTheYear_InAnyRevision(int major)
    {
        var file = MemoryFileDataLoader.Mp3(Tag(major, [new Frame("TYER", Latin1Text("1991")), new Frame("TDAT", Latin1Text("0201"))]));

        Assert.That(Data(Resolve<decimal>("id3v2::year", file)), Is.EqualTo(new object[] { 1991m }));
    }

    [Test]
    public void TextThatDoesNotDecode_IsUnusable()
    {
        Assert.That(Data(Text("artist", 4, ("TPE1", [3, 0x4D, 0xF6, 0x72]))), Is.EqualTo(new[] { "unusable" }));
    }

    [Test]
    public void AFileWithNoId3v2Tag_HasEveryId3v2ConceptAbsent()
    {
        var file = MemoryFileDataLoader.Mp3([], Id3v1(artist: "x"));

        Assert.That(Resolve<string>("id3v2::artist", file).IsAbsent, Is.True);
        Assert.That(Resolve<decimal>("id3v2::year", file).IsAbsent, Is.True);
    }

    // --- APE ---

    [Test]
    public void ApeConcepts_ReadTheItemsByTheirConventionalKeys()
    {
        var file = MemoryFileDataLoader.Mp3([], Ape(ApeText("ARTIST", "AC/DC", " Motörhead "), ApeText("Year", "1991-01-02"), ApeText("track", "3/12"), ApeText("Genre", "17")));

        Assert.That(Data(Resolve<string>("ape::artist", file)), Is.EqualTo(new[] { "AC/DC", "Motörhead" }));
        Assert.That(Data(Resolve<decimal>("ape::year", file)), Is.EqualTo(new object[] { 1991m }));
        Assert.That(Data(Resolve<decimal>("ape::track", file)), Is.EqualTo(new object[] { 3m }));
        Assert.That(Data(Resolve<string>("ape::genre", file)), Is.EqualTo(new[] { "17" }), "APE applies no genre conventions");
    }

    [Test]
    public void ABinaryApeItem_IsUnusableThroughAConcept()
    {
        var file = MemoryFileDataLoader.Mp3([], Ape(new ApeItem("Title", [1, 2, 3], Binary: true)));

        Assert.That(Data(Resolve<string>("ape::title", file)), Is.EqualTo(new[] { "unusable" }));
    }

    // --- Id3v1 ---

    [Test]
    public void Id3v1Text_IsTrimmed_AndAPaddedFieldIsAbsent()
    {
        var file = MemoryFileDataLoader.Mp3([], Id3v1(title: " Title ", artist: ""));

        Assert.That(Data(Resolve<string>("id3v1::title", file)), Is.EqualTo(new[] { "Title" }));
        Assert.That(Resolve<string>("id3v1::artist", file).IsAbsent, Is.True);
    }

    [TestCase((byte)0)]
    [TestCase((byte)' ')]
    public void APaddedYear_IsAbsent_WhicheverPaddingWasUsed(byte padding)
    {
        Assert.That(Resolve<decimal>("id3v1::year", MemoryFileDataLoader.Mp3([], Id3v1(padding: padding))).IsAbsent, Is.True);
    }

    [TestCase("1991", 1991)]
    [TestCase("0000", null)]
    [TestCase("abcd", null)]
    [TestCase("\t\t\t\t", null)]
    public void AnId3v1Year_IsDateShaped(string recorded, int? expected)
    {
        var value = Resolve<decimal>("id3v1::year", MemoryFileDataLoader.Mp3([], Id3v1(year: recorded)));

        Assert.That(Data(value), Is.EqualTo(new object[] { expected is { } year ? (decimal)year : "unusable" }));
    }

    [TestCase((byte)0, "Blues")]
    [TestCase((byte)17, "Rock")]
    [TestCase((byte)147, "Synthpop")]
    [TestCase((byte)200, "unusable")]
    public void AnId3v1GenreByte_NamesATableEntry(byte genre, string expected)
    {
        Assert.That(Data(Resolve<string>("id3v1::genre", MemoryFileDataLoader.Mp3([], Id3v1(genre: genre)))), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void Id3v1Genre255_IsAbsent()
    {
        Assert.That(Resolve<string>("id3v1::genre", MemoryFileDataLoader.Mp3([], Id3v1(genre: 255))).IsAbsent, Is.True);
    }

    [Test]
    public void AnId3v1TrackByte_IsTheTrack_ZeroBeingAbsent_AndV10HavingNone()
    {
        Assert.That(Data(Resolve<decimal>("id3v1::track", MemoryFileDataLoader.Mp3([], Id3v1(track: 3)))), Is.EqualTo(new object[] { 3m }));
        Assert.That(Resolve<decimal>("id3v1::track", MemoryFileDataLoader.Mp3([], Id3v1(track: 0))).IsAbsent, Is.True);
        Assert.That(Resolve<decimal>("id3v1::track", MemoryFileDataLoader.Mp3([], Id3v1())).IsAbsent, Is.True);
    }

    [Test]
    public void Id3v1Text_IsLatin1_AndALongValueIsCutAtThirtyBytes()
    {
        var file = MemoryFileDataLoader.Mp3([], Id3v1(artist: "Motörhead", album: new string('x', 40)));

        Assert.That(Data(Resolve<string>("id3v1::artist", file)), Is.EqualTo(new[] { "Motörhead" }));
        Assert.That(Data(Resolve<string>("id3v1::album", file)), Is.EqualTo(new[] { new string('x', 30) }));
    }

    // --- vorbis ---

    [Test]
    public void VorbisConcepts_AreAbsentFromAnMp3()
    {
        Assert.That(Resolve<string>("vorbis::artist", Id3v2(4, ("TPE1", Latin1Text("x")))).IsAbsent, Is.True);
    }
}
