using System.Collections.Immutable;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;
using Goro.Reading.Bytes;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Predicates.Identifiers;

/// <summary>
/// The source functions, bound to the files of the corpus: what <c>field()</c> and <c>bytes()</c>
/// give for each source, as "Absent, usable, and unusable data" and the sections on each source in
/// docs/features/builtins/identifiers.md describe.
/// </summary>
public class TagSourceFunctionTests
{
    private static readonly Origin Origin = new(new SourceId(0), "call", 0);

    private static Value<T> Call<T>(string file, string source, string function, params string[] arguments) where T : notnull
    {
        var found = (SourceFunctionLookup.Found)BuiltInCatalog.Instance.LookupFunction(source, function);
        var resolution = found.Function.Resolve([.. arguments]);
        Assert.That(resolution, Is.InstanceOf<SourceFunctionResolution.Found>(), $"{source}::{function}({string.Join(", ", arguments)})");
        var declaration = (IdentifierDeclaration<T>)((SourceFunctionResolution.Found)resolution).Declaration;
        var path = AudioCorpus.PathOf(file);
        using var loader = new FileDataLoader(path, ReadPolicy.Default);
        return declaration.Binding.Resolve(new FileData(path, loader), Origin);
    }

    private static Value<string> Field(string file, string source, params string[] arguments) => Call<string>(file, source, "field", arguments);

    private static Value<Blob> Bytes(string file, string source, string name) => Call<Blob>(file, source, "bytes", name);

    private static IEnumerable<string> Texts(Value<string> value) =>
        value.Occurrences.Select(o => o is Usable<string>(var text) ? text : throw new AssertionException($"{value} holds an unusable occurrence"));

    private static readonly Occurrence<string> UnusableText = new Unusable<string>(Origin);

    // --- id3v2 ---

    [Test]
    public void Id3v2Field_GivesEveryValueOfTheFrame()
    {
        Assert.That(Texts(Field("mp3/id3v24.mp3", "id3v2", "TPE1")), Is.EquivalentTo(new[] { "AC/DC", "Motörhead" }));
    }

    [Test]
    public void Id3v2Field_IsMatchedWhateverTheCaseItIsWrittenIn()
    {
        Assert.That(Texts(Field("mp3/id3v24.mp3", "id3v2", "tit2")), Is.EqualTo(new[] { "Title ✓ – Ünïcødé" }));
    }

    [Test]
    public void Id3v2Field_WithADescription_ReadsTheFramesWhoseDescriptionMatchesWithoutRegardToCase()
    {
        Assert.That(Texts(Field("mp3/id3v24.mp3", "id3v2", "TXXX", "MOOD")), Is.EquivalentTo(new[] { "calm", "wistful" }));
        Assert.That(Texts(Field("mp3/id3v24.mp3", "id3v2", "COMM", "")), Is.EqualTo(new[] { "A comment" }));
        Assert.That(Texts(Field("mp3/id3v24.mp3", "id3v2", "COMM")), Is.EquivalentTo(new[] { "A comment", " 00000A2F 0000096E" }));
        Assert.That(Field("mp3/id3v24.mp3", "id3v2", "TXXX", "nothing like it").IsAbsent, Is.True);
    }

    [Test]
    public void Id3v2Field_ReadsAFrameRecordedUnderAnEarlierRevisionsName()
    {
        Assert.That(Texts(Field("mp3/id3v22.mp3", "id3v2", "TIT2")), Is.EqualTo(new[] { "Title v2.2" }));
        Assert.That(Texts(Field("mp3/id3v23.mp3", "id3v2", "TDRC")), Is.EqualTo(new[] { "1991" }), "TYER is read as TDRC");
    }

    [Test]
    public void Id3v2Field_ReadsApplesTextFrames()
    {
        Assert.That(Texts(Field("mp3/rw-apple-text-frames.mp3", "id3v2", "GRP1")), Is.EqualTo(new[] { "Grouping" }));
        Assert.That(Texts(Field("mp3/rw-apple-text-frames.mp3", "id3v2", "MVIN")), Is.EqualTo(new[] { "1/4" }));
    }

    [Test]
    public void Id3v2Field_OfTextThatDoesNotDecode_IsOneUnusableOccurrence()
    {
        Assert.That(Field("mp3/dmg-id3v24-invalid-utf8.mp3", "id3v2", "TPE1").Occurrences, Is.EqualTo(new[] { UnusableText }));
    }

    [Test]
    public void Id3v2Field_OfAFrameRunningPastItsTag_IsUnusable_AndWhatLayBeyondItIsAbsent()
    {
        Assert.That(Field("mp3/dmg-id3v24-frame-overrun.mp3", "id3v2", "TCON").Occurrences, Is.EqualTo(new[] { UnusableText }));
        Assert.That(Texts(Field("mp3/dmg-id3v24-frame-overrun.mp3", "id3v2", "TIT2")), Is.Not.Empty);
    }

    [Test]
    public void Id3v2Field_ReadsOnlyTheFirstOfSeveralTags()
    {
        Assert.That(Texts(Field("mp3/rw-three-id3v2-tags.mp3", "id3v2", "TIT2")), Is.EqualTo(new[] { "First tag title" }));
    }

    [TestCase("mp3/id3v24.mp3", "TPE4", TestName = "a frame the tag does not hold is absent")]
    [TestCase("mp3/ape2.mp3", "TIT2", TestName = "a file with no Id3v2 tag has every frame absent")]
    [TestCase("mp3/dmg-id3v2-version-5.mp3", "TIT2", TestName = "a tag that cannot be read at all is absent")]
    public void Id3v2Field_IsAbsent(string file, string frame)
    {
        Assert.That(Field(file, "id3v2", frame).IsAbsent, Is.True);
    }

    [Test]
    public void Id3v2Bytes_GivesTheContentOfEachFrame_AsRecorded()
    {
        var pictures = Bytes("mp3/id3v24.mp3", "id3v2", "APIC");

        Assert.That(pictures.Occurrences, Has.Length.EqualTo(1).And.All.InstanceOf<Usable<Blob>>());
        Assert.That(Bytes("mp3/id3v23-encrypted.mp3", "id3v2", "TPE1").Occurrences, Is.EqualTo(new[] { new Unusable<Blob>(Origin) }), "an encrypted frame");
    }

    // --- ape ---

    [Test]
    public void ApeField_GivesEveryValueOfTheItem_MatchedWithoutRegardToCase()
    {
        Assert.That(Texts(Field("mp3/ape2.mp3", "ape", "artist")), Is.EqualTo(new[] { "AC/DC", "Motörhead" }));
        Assert.That(Texts(Field("mp3/ape2.mp3", "ape", "Album Artist")), Is.EqualTo(new[] { "Various" }));
    }

    [Test]
    public void ApeBytes_NeverSplitsAValue()
    {
        Assert.That(Bytes("mp3/ape2.mp3", "ape", "Artist").Occurrences, Has.Length.EqualTo(1).And.All.InstanceOf<Usable<Blob>>());
    }

    [Test]
    public void ABinaryApeItem_IsUnusableThroughField_AndABlobThroughBytes()
    {
        Assert.That(Field("mp3/ape2.mp3", "ape", "Cover Art (Front)").Occurrences, Is.EqualTo(new[] { UnusableText }));
        Assert.That(Bytes("mp3/ape2.mp3", "ape", "Cover Art (Front)").Occurrences, Has.Length.EqualTo(1).And.All.InstanceOf<Usable<Blob>>());
    }

    [Test]
    public void ApeKeysDifferingOnlyInCase_GiveOnlyTheLast()
    {
        var file = "mp3/ape2-keys-differing-in-case.mp3";
        var last = AudioCorpus.Analyse(file).Source(Goro.Reading.Tags.TagFormat.Ape).Fields.Last(f => string.Equals(f.Key, "artist", StringComparison.OrdinalIgnoreCase));
        var expected = AudioCorpus.Analyse(file).Text(last);

        Assert.That(Texts(Field(file, "ape", "ARTIST")), Is.EqualTo(expected));
        Assert.That(Texts(Field(file, "ape", "Artist")), Is.EqualTo(expected));
    }

    [Test]
    public void ApeV1Text_IsReadAsLatin1()
    {
        Assert.That(Texts(Field("mp3/ape1-latin1.mp3", "ape", "Title")), Is.EqualTo(new[] { "Motörhead" }));
    }

    // --- vorbis ---

    [Test]
    public void VorbisComments_AreAbsentFromAnMp3()
    {
        Assert.That(Field("mp3/id3v24.mp3", "vorbis", "TITLE").IsAbsent, Is.True);
        Assert.That(Bytes("mp3/id3v24.mp3", "vorbis", "TITLE").IsAbsent, Is.True);
    }

    // --- a file with no audio ---

    [TestCase("id3v2", "field", "TIT2")]
    [TestCase("ape", "bytes", "Title")]
    [TestCase("vorbis", "field", "TITLE")]
    public void AFileWithNoAudio_IsUnreadable_WhicheverSourceIsAsked(string source, string function, string name)
    {
        TestDelegate call = function == "field"
            ? () => Field("mp3/dmg-id3v24-truncated-in-tag.mp3", source, name)
            : () => Bytes("mp3/dmg-id3v24-truncated-in-tag.mp3", source, name);

        Assert.That(call, Throws.TypeOf<UnreadableFileException>().With.Property(nameof(UnreadableFileException.Reason)).EqualTo("no MPEG audio found"));
    }

    [Test]
    public void FieldOfApplesFrames_IsNoLongerAnError()
    {
        var found = (SourceFunctionLookup.Found)BuiltInCatalog.Instance.LookupFunction("id3v2", "field");

        Assert.That(found.Function.Resolve(ImmutableArray.Create("GRP1")), Is.InstanceOf<SourceFunctionResolution.Found>());
    }
}
