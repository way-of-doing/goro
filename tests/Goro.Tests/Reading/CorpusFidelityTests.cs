using Goro.Reading;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// Faithfulness of the reader (docs/testing.md, "Reading audio files"): every field the manifest
/// records for a healthy MP3 is found under the name Goro reads it by, its text is what the manifest
/// says the format records, and its content is the bytes as recorded with the storage transformations
/// undone.
/// </summary>
public class CorpusFidelityTests
{
    private static IEnumerable<TestCaseData> Mp3EntriesWithFields() =>
        AudioCorpus.Manifest
            .Where(entry => entry.File.StartsWith("mp3/", StringComparison.Ordinal) && entry.Recorded is { Count: > 0 } recorded && recorded.Any(r => r.Key != "*"))
            .Select(entry => new TestCaseData(entry.File).SetName($"fidelity: {entry.File}"));

    [TestCaseSource(nameof(Mp3EntriesWithFields))]
    public void EveryRecordedField_IsReadAsRecorded(string file)
    {
        var entry = AudioCorpus.Manifest.Single(e => e.File == file);
        var analysed = AudioCorpus.Analyse(file);
        var claimed = new HashSet<FieldEntry>(ReferenceEqualityComparer.Instance);

        Assert.Multiple(() =>
        {
            foreach (var recorded in entry.Recorded!.Where(r => r.Key != "*"))
            {
                var kind = KindOf(recorded.Tag);
                var tag = analysed.Layout.Tags.FirstOrDefault(t => t.Kind == kind && t.IsSource);
                Assert.That(tag, Is.Not.Null, $"{recorded.Tag} tag");
                if (tag is null)
                {
                    continue;
                }

                var key = kind.Format == TagFormat.Id3v2 ? Id3v2FrameNames.Canonical(recorded.Key) : recorded.Key;
                var field = tag.Fields.FirstOrDefault(f => f.Key == key && !claimed.Contains(f) && DescriptionMatches(analysed.Values, f, recorded.Description));
                Assert.That(field, Is.Not.Null, $"{recorded.Tag} {recorded.Key} {recorded.Description}");
                if (field is null)
                {
                    continue;
                }

                claimed.Add(field);
                var label = $"{recorded.Key}{(recorded.Description is null ? "" : $" ({recorded.Description})")}";
                if (recorded.Note?.Contains("unusable", StringComparison.Ordinal) == true)
                {
                    Assert.That(analysed.Values.Bytes(field), Is.InstanceOf<FieldContent.Unreadable>(), $"{label}: bytes()");
                    Assert.That(analysed.Values.Text(field), Is.InstanceOf<FieldText.Unreadable>(), $"{label}: field()");
                    continue;
                }

                Assert.That(HexOf(analysed.Values.Bytes(field)), Is.EqualTo(recorded.BytesHex), $"{label}: bytes()");
                if (recorded.Values is null)
                {
                    Assert.That(analysed.Values.Text(field), Is.InstanceOf<FieldText.NotText>(), $"{label}: holds no text");
                }
                else
                {
                    Assert.That(analysed.Text(field), Is.EqualTo(recorded.Values), $"{label}: field()");
                }
            }
        });
    }

    [Test]
    public void OfSeveralId3v2Tags_OnlyTheFirstIsTheSource()
    {
        var analysed = AudioCorpus.Analyse("mp3/id3v2-twice.mp3");

        var id3v2 = analysed.Layout.Tags.Where(t => t.Kind.Format == TagFormat.Id3v2).ToList();
        Assert.That(id3v2.Select(t => t.IsSource), Is.EqualTo(new[] { true, false }));
        Assert.That(analysed.Layout.Conditions.OfType<FurtherTag>().Select(c => c.Tag), Is.EqualTo(new[] { id3v2[1].Kind }));
        Assert.That(analysed.Layout.IsIncomplete, Is.False, "a second tag is not damage");
    }

    [Test]
    public void ThreeId3v2Tags_TheFirstIsTheSource_AndTheOthersAreReported()
    {
        var analysed = AudioCorpus.Analyse("mp3/rw-three-id3v2-tags.mp3");

        Assert.That(analysed.Layout.Tags.Count(t => t.Kind.Format == TagFormat.Id3v2), Is.EqualTo(3));
        Assert.That(analysed.Layout.Conditions.OfType<FurtherTag>().Count(), Is.EqualTo(2));
        Assert.That(analysed.Text(analysed.Field(TagFormat.Id3v2, "TIT2")), Is.EqualTo(new[] { "First tag title" }));
    }

    [TestCase("mp3/id3v10.mp3", 0, false)]
    [TestCase("mp3/id3v11.mp3", 1, true)]
    public void Id3v1_TellsTheRevisionsApartByTheTrackByte(string file, int revision, bool hasTrack)
    {
        var analysed = AudioCorpus.Analyse(file);

        var tag = analysed.Source(TagFormat.Id3v1);
        Assert.That(tag.Kind.Revision, Is.EqualTo(revision));
        Assert.That(tag.Fields.Any(f => f.Key == "track"), Is.EqualTo(hasTrack));
        Assert.That(analysed.Values.Bytes(analysed.Field(TagFormat.Id3v1, "genre")), Is.EqualTo(new FieldContent.Readable(new byte[] { 17 })).Using<FieldContent>(SameContent));
    }

    [Test]
    public void Id3v1Text_IsLatin1_WithoutItsPadding()
    {
        var analysed = AudioCorpus.Analyse("mp3/id3v11-latin1-genre255.mp3");

        var title = analysed.Text(analysed.Field(TagFormat.Id3v1, "title"));
        Assert.That(title, Has.Count.EqualTo(1));
        Assert.That(title![0].EndsWith(' ') || title[0].Contains('\0'), Is.False, "padding left in");
        Assert.That(title[0].Any(c => c > 0x7F), Is.True, "the fixture holds Latin-1 text outside ASCII");
        Assert.That(HexOf(analysed.Values.Bytes(analysed.Field(TagFormat.Id3v1, "genre"))), Is.EqualTo("ff"));
    }

    [Test]
    public void V22NamesInAV23Tag_AreDeferred_SoTheTagYieldsNothing()
    {
        var analysed = AudioCorpus.Analyse("mp3/rw-v22-names-in-v23-tag.mp3");

        var tag = analysed.Source(TagFormat.Id3v2);
        Assert.That(tag.Fields, Is.Empty);
        Assert.That(tag.State, Is.EqualTo(TagState.BrokenOff));
    }

    private static bool SameContent(FieldContent a, FieldContent b) => HexOf(a) == HexOf(b);

    private static bool DescriptionMatches(TagValues values, FieldEntry field, string? description) =>
        description is null
            ? values.Description(field) is FieldDescription.None
            : values.Description(field) is FieldDescription.Readable(var text, _) && text == description;

    private static string? HexOf(FieldContent content) =>
        content is FieldContent.Readable(var bytes) ? Convert.ToHexStringLower(bytes.Span) : null;

    private static TagKind KindOf(string tag) => tag switch
    {
        "id3v2.2" => new(TagFormat.Id3v2, 2),
        "id3v2.3" => new(TagFormat.Id3v2, 3),
        "id3v2.4" => new(TagFormat.Id3v2, 4),
        "ape" => new(TagFormat.Ape, 2),
        "ape1" => new(TagFormat.Ape, 1),
        _ => throw new ArgumentException($"No tag kind for {tag}."),
    };
}
