using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;
using static Goro.Tests.TestSupport.Id3v2Bytes;

namespace Goro.Tests.Reading;

/// <summary>
/// How Id3v2 stores a frame, and the limits on reading one: the shapes and edges the corpus's
/// fidelity test does not reach on its own (docs/testing.md, "Reading audio files").
/// </summary>
public class Id3v2StorageTests
{
    [Test]
    public void ADescriptionThatDoesNotDecode_MakesTheFrameUnusableWithOrWithoutADescription()
    {
        byte[] invalidUtf8 = [0xC3, 0x28];
        var tag = Tag(4, [new Frame("TXXX", [3, .. invalidUtf8, 0, .. Utf8("value")])]);
        var analysed = AudioCorpus.Analyse(Mp3(tag));
        var frame = analysed.Field(TagFormat.Id3v2, "TXXX");

        Assert.That(analysed.Values.Description(frame), Is.EqualTo(new FieldDescription.Unreadable(UnreadableReason.DoesNotDecode)));
        Assert.That(analysed.Values.Text(frame), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.DoesNotDecode)));
    }

    [Test]
    public void ACommentTooShortForItsLanguage_IsUnusable()
    {
        var analysed = AudioCorpus.Analyse(Mp3(Tag(4, [new Frame("COMM", [3, (byte)'e', (byte)'n'])])));
        var frame = analysed.Field(TagFormat.Id3v2, "COMM");

        Assert.That(analysed.Values.Text(frame), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.NoLanguage)));
        Assert.That(analysed.Values.Description(frame), Is.EqualTo(new FieldDescription.Unreadable(UnreadableReason.NoLanguage)));
    }

    [Test]
    public void ADescriptionWithNoTerminator_IsUnusable()
    {
        var analysed = AudioCorpus.Analyse(Mp3(Tag(4, [new Frame("TXXX", [3, .. Utf8("no end")])])));

        Assert.That(analysed.Values.Text(analysed.Field(TagFormat.Id3v2, "TXXX")), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.DescriptionUnterminated)));
    }

    [Test]
    public void ATextFrameWithATrailingTerminator_HoldsOneValue_AndAnEmptyOneHoldsTheEmptyString()
    {
        var analysed = AudioCorpus.Analyse(Mp3(Tag(4, [new Frame("TIT2", [3, .. Utf8("one"), 0]), new Frame("TPE2", [3])])));

        Assert.That(analysed.Text(analysed.Field(TagFormat.Id3v2, "TIT2")), Is.EqualTo(new[] { "one" }));
        Assert.That(analysed.Text(analysed.Field(TagFormat.Id3v2, "TPE2")), Is.EqualTo(new[] { "" }));
    }

    [Test]
    public void AUrlFrame_IsLatin1Text_AndWxxxCarriesADescription()
    {
        var analysed = AudioCorpus.Analyse(Mp3(Tag(4,
        [
            new Frame("WOAR", Utf8("https://example.com/a")),
            new Frame("WXXX", [0, .. Utf8("home"), 0, .. Utf8("https://example.com/b")]),
        ])));

        Assert.That(analysed.Text(analysed.Field(TagFormat.Id3v2, "WOAR")), Is.EqualTo(new[] { "https://example.com/a" }));
        var wxxx = analysed.Field(TagFormat.Id3v2, "WXXX");
        Assert.That(analysed.Values.Description(wxxx), Is.EqualTo(new FieldDescription.Readable("home")));
        Assert.That(analysed.Text(wxxx), Is.EqualTo(new[] { "https://example.com/b" }));
    }

    [Test]
    public void AFrameThatHoldsNoText_HasNoText_AndItsBytesAreItsContent()
    {
        byte[] content = [1, 2, 3, 0xFF];
        var analysed = AudioCorpus.Analyse(Mp3(Tag(4, [new Frame("PRIV", content)])));
        var frame = analysed.Field(TagFormat.Id3v2, "PRIV");

        Assert.That(analysed.Values.Text(frame), Is.InstanceOf<FieldText.NotText>());
        Assert.That(((FieldContent.Readable)analysed.Values.Bytes(frame)).Bytes.ToArray(), Is.EqualTo(content));
    }

    [Test]
    public void FlagPrefixesLongerThanTheFrame_MakeItUnusable()
    {
        // v2.4: a data length indicator (flag p) promises four bytes in front of the content.
        var analysed = AudioCorpus.Analyse(Mp3(Tag(4, [new Frame("TIT2", [3, 0x41], Flags: 0x0001)])));

        Assert.That(analysed.Values.Bytes(analysed.Field(TagFormat.Id3v2, "TIT2")), Is.EqualTo(new FieldContent.Unreadable(UnreadableReason.ShorterThanItsFlags)));
    }

    [Test]
    public void AV22FrameName_IsReadUnderItsV24Name()
    {
        var analysed = AudioCorpus.Analyse("mp3/id3v22.mp3");

        Assert.That(analysed.Source(TagFormat.Id3v2).Fields.Select(f => f.Key), Is.SupersetOf(new[] { "TIT2", "TPE1", "TCON", "TDRC", "TXXX", "COMM", "PIC" }));
    }

    // --- The limits ---

    [Test]
    public void DecompressingPastThePayloadLimit_IsUnusable()
    {
        var policy = new ReadPolicy(PayloadLimit: 64);
        var analysed = AudioCorpus.Analyse("mp3/id3v23-compressed.mp3", policy);
        var compressed = analysed.Source(TagFormat.Id3v2).Fields.Single(f => f.Transform.Compressed);

        Assert.That(analysed.Values.Bytes(compressed), Is.EqualTo(new FieldContent.Unreadable(UnreadableReason.TooLarge)));
        Assert.That(analysed.Values.Text(analysed.Field(TagFormat.Id3v2, "TIT2")), Is.InstanceOf<FieldText.Readable>(), "the frames around it are not affected");
    }

    [Test]
    public void AValueOutsideTheWindowsAndOverThePayloadLimit_IsUnusable_AndIsNotRead()
    {
        var policy = new ReadPolicy(HeadWindow: 16, TailWindow: 16, PayloadLimit: 32);
        var analysed = AudioCorpus.Analyse("mp3/id3v24.mp3", policy);
        var picture = analysed.Field(TagFormat.Id3v2, "APIC");

        Assert.That(analysed.Values.Bytes(picture), Is.EqualTo(new FieldContent.Unreadable(UnreadableReason.TooLarge)));
        Assert.That(analysed.Layout.Reads.Records.Last(), Has.Property(nameof(ReadRecord.Outcome)).EqualTo(ReadOutcome.OverLimit));
    }

    [Test]
    public void ATagUnsynchronisedAsAWhole_OverTheStructureBudget_IsUnusable_AndTheAudioIsStillFound()
    {
        var policy = new ReadPolicy(HeadWindow: 16, TailWindow: 16, StructureBudget: 64);
        var analysed = AudioCorpus.Analyse("mp3/id3v23-unsync.mp3", policy);

        Assert.That(analysed.Source(TagFormat.Id3v2).State, Is.EqualTo(TagState.Unusable));
        Assert.That(analysed.Layout.Conditions.OfType<TagUnusable>().Single().Reason, Is.EqualTo(TagUnusableReason.TooLargeToResynchronise));
        Assert.That(analysed.Layout.Audio, Is.InstanceOf<AudioLocation.Found>());
    }

    [Test]
    public void ATagUnsynchronisedAsAWhole_HasItsFramesReadFromTheResynchronisedTag()
    {
        var analysed = AudioCorpus.Analyse("mp3/id3v23-unsync.mp3");

        Assert.That(analysed.Source(TagFormat.Id3v2).Fields, Has.All.Property(nameof(FieldEntry.Stored)).InstanceOf<StoredBytes.InMemory>());
    }
}
