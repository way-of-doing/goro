using Goro.Reading;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// What damaged tag data yields (docs/testing.md, "Reading audio files", and "Damaged data" in
/// docs/features/builtins/identifiers.md): an unusable occurrence where a datum is clearly recorded
/// and cannot be read, nothing beyond a break, nothing at all from a tag that cannot be read, the
/// rest of the file unaffected, and the file reported as incomplete exactly when a structure is
/// broken or a tag unusable. No damage makes the audio unfindable unless the file ends inside its tag.
/// </summary>
public class DamageTests
{
    // --- A datum that cannot be read: unusable, its neighbours intact, the file not incomplete ---

    [TestCase("mp3/dmg-id3v24-invalid-utf8.mp3", "TPE1", UnreadableReason.DoesNotDecode)]
    [TestCase("mp3/dmg-id3v24-utf16-odd-length.mp3", "TIT2", UnreadableReason.DoesNotDecode)]
    [TestCase("mp3/dmg-id3v24-bad-encoding.mp3", "TIT2", UnreadableReason.UnknownEncoding)]
    public void TextThatDoesNotDecode_IsUnusableThroughField_ButItsBytesAreThere(string file, string frame, UnreadableReason reason)
    {
        var analysed = AudioCorpus.Analyse(file);
        var field = analysed.Field(TagFormat.Id3v2, frame);

        Assert.That(analysed.Values.Text(field), Is.EqualTo(new FieldText.Unreadable(reason)));
        Assert.That(analysed.Values.Bytes(field), Is.InstanceOf<FieldContent.Readable>());
        AssertOthersReadable(analysed, TagFormat.Id3v2, except: field);
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    [Test]
    public void TextNeverComesBackHoldingReplacementCharacters()
    {
        foreach (var file in AudioCorpus.Mp3Files)
        {
            var analysed = AudioCorpus.Analyse(file);
            foreach (var field in analysed.Layout.Tags.SelectMany(tag => tag.Fields))
            {
                if (analysed.Values.Text(field) is FieldText.Readable(var values, _))
                {
                    Assert.That(values, Has.None.Contains('�'), $"{file}: {field.Key}");
                }
            }
        }
    }

    [Test]
    public void ZlibThatDoesNotInflate_IsUnusableThroughBothFieldAndBytes()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v24-bad-zlib.mp3");
        var compressed = analysed.Source(TagFormat.Id3v2).Fields.Single(f => f.Transform.Compressed);

        Assert.That(analysed.Values.Bytes(compressed), Is.EqualTo(new FieldContent.Unreadable(UnreadableReason.DoesNotDecompress)));
        Assert.That(analysed.Values.Text(compressed), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.DoesNotDecompress)));
        AssertOthersReadable(analysed, TagFormat.Id3v2, except: compressed);
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    [Test]
    public void AZeroSizeFrame_IsUnusable_AndTheFramesAfterItAreRead()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v24-zero-size-frame.mp3");
        var tag = analysed.Source(TagFormat.Id3v2);
        var empty = analysed.Field(TagFormat.Id3v2, "TIT3");

        Assert.That(analysed.Values.Text(empty), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.NoEncoding)));
        Assert.That(tag.Fields.IndexOf(empty), Is.LessThan(tag.Fields.Length - 1), "frames follow the empty one");
        Assert.That(tag.State, Is.EqualTo(TagState.Intact));
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    [Test]
    public void ApeTextThatIsNotUtf8_IsUnusableThroughField_ButItsBytesAreThere()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-ape-invalid-utf8.mp3");
        var unreadable = analysed.Source(TagFormat.Ape).Fields.Where(f => analysed.Values.Text(f) is FieldText.Unreadable).ToList();

        Assert.That(unreadable, Has.Count.EqualTo(1));
        Assert.That(analysed.Values.Text(unreadable[0]), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.DoesNotDecode)));
        Assert.That(analysed.Values.Bytes(unreadable[0]), Is.InstanceOf<FieldContent.Readable>());
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    // --- A broken structure: what precedes the break intact, nothing after it, the file incomplete ---

    [Test]
    public void AFrameRunningPastItsTag_IsUnusable_ThoseBeforeItIntact_NothingAfter()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v24-frame-overrun.mp3");
        var tag = analysed.Source(TagFormat.Id3v2);

        Assert.That(tag.State, Is.EqualTo(TagState.BrokenOff));
        Assert.That(tag.Fields.Select(f => f.Key), Is.EqualTo(new[] { "TIT2", "TPE1", "TALB", "TCON" }), "the fourth frame overruns, and is the last found");
        var overrun = tag.Fields[^1];
        Assert.That(analysed.Values.Bytes(overrun), Is.EqualTo(new FieldContent.Unreadable(UnreadableReason.RunsPastTag)));
        Assert.That(analysed.Values.Text(overrun), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.RunsPastTag)));
        AssertOthersReadable(analysed, TagFormat.Id3v2, except: overrun);
        AssertIncomplete(analysed, StructureBreak.FieldRunsPastTag);
        Assert.That(analysed.Layout.Audio, Is.InstanceOf<AudioLocation.Found>());
    }

    // The fixture zeroes 64 bytes from offset 120, part way through a frame: that frame's own content
    // is damaged with nothing to say so, the next header reads as padding, and the data after it is
    // what shows the frames did not really end there.
    [Test]
    public void AZeroedBlockInATag_EndsWhatCanBeRead_AndIsReported()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v24-zeroed-block.mp3");
        var tag = analysed.Source(TagFormat.Id3v2);

        Assert.That(tag.State, Is.EqualTo(TagState.BrokenOff));
        var before = tag.Fields.Where(f => ((StoredBytes.InFile)f.Stored).Region.End <= 120).ToList();
        Assert.That(before, Is.Not.Empty);
        Assert.That(before.Select(f => analysed.Values.Text(f)), Has.All.InstanceOf<FieldText.Readable>());
        AssertIncomplete(analysed, StructureBreak.DataAfterPadding);
    }

    [Test]
    public void ATagSizeTooShort_BreaksTheTagInsideAFrame_AndTheAudioIsStillFoundBehindIt()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v2-size-short.mp3");

        Assert.That(analysed.Source(TagFormat.Id3v2).State, Is.EqualTo(TagState.BrokenOff));
        AssertIncomplete(analysed, StructureBreak.FieldRunsPastTag);
        Assert.That(analysed.Layout.Audio, Is.InstanceOf<AudioLocation.Found>());
        Assert.That(analysed.Layout.Conditions.OfType<JunkBeforeAudio>(), Is.Not.Empty, "the rest of the tag lies where the audio should start");
    }

    [Test]
    public void AnApeItemCountTooHigh_KeepsTheItemsThatFit()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-ape-item-count-high.mp3");
        var tag = analysed.Source(TagFormat.Ape);

        Assert.That(tag.State, Is.EqualTo(TagState.BrokenOff));
        Assert.That(tag.Fields, Is.Not.Empty);
        AssertOthersReadable(analysed, TagFormat.Ape);
        AssertIncomplete(analysed, StructureBreak.ItemCountTooHigh);
    }

    [Test]
    public void AnApeItemRunningPastItsTag_IsUnusable()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-ape-item-overrun.mp3");
        var tag = analysed.Source(TagFormat.Ape);
        var overrun = tag.Fields.Single(f => f.RunsPastTag);

        Assert.That(tag.State, Is.EqualTo(TagState.BrokenOff));
        Assert.That(analysed.Values.Bytes(overrun), Is.EqualTo(new FieldContent.Unreadable(UnreadableReason.RunsPastTag)));
        AssertOthersReadable(analysed, TagFormat.Ape, except: overrun);
        AssertIncomplete(analysed, StructureBreak.FieldRunsPastTag);
    }

    // --- A tag that cannot be read at all: absent everywhere, other tags and the audio unaffected ---

    [TestCase("mp3/dmg-id3v2-size-past-eof.mp3", TagUnusableReason.SizeOutsideFile)]
    [TestCase("mp3/dmg-id3v2-size-not-syncsafe.mp3", TagUnusableReason.SizeNotSyncsafe)]
    [TestCase("mp3/dmg-id3v2-version-5.mp3", TagUnusableReason.UnknownVersion)]
    public void AnId3v2TagThatCannotBeRead_IsAbsent_AndTheAudioIsStillFound(string file, TagUnusableReason reason)
    {
        var analysed = AudioCorpus.Analyse(file);
        var tag = analysed.Source(TagFormat.Id3v2);

        Assert.That(tag.State, Is.EqualTo(TagState.Unusable));
        Assert.That(tag.Fields, Is.Empty);
        Assert.That(analysed.Layout.Conditions.OfType<TagUnusable>().Select(c => c.Reason), Is.EqualTo(new[] { reason }));
        Assert.That(analysed.Layout.IsIncomplete, Is.True);
        Assert.That(analysed.Layout.Audio, Is.InstanceOf<AudioLocation.Found>());
    }

    [Test]
    public void AnApeTagWhoseSizeReachesBeforeTheStart_IsAbsent()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-ape-size-past-start.mp3");

        Assert.That(analysed.Source(TagFormat.Ape).State, Is.EqualTo(TagState.Unusable));
        Assert.That(analysed.Layout.Conditions.OfType<TagUnusable>().Single().Reason, Is.EqualTo(TagUnusableReason.SizeOutsideFile));
        Assert.That(analysed.Layout.Audio, Is.InstanceOf<AudioLocation.Found>());
    }

    [Test]
    public void AFileEndingInsideItsId3v2Tag_HasNoAudio()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v24-truncated-in-tag.mp3");

        Assert.That(analysed.Layout.Audio, Is.EqualTo(new AudioLocation.NotFound(AudioNotFoundReason.NoFrame)));
        Assert.That(analysed.Source(TagFormat.Id3v2).State, Is.EqualTo(TagState.Unusable));
    }

    // --- Shapes read as the writer meant ---

    [Test]
    public void FrameSizesWrittenAsPlainIntegers_AreReadAsSuch_EveryFrameIntact()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v24-itunes-sizes.mp3");

        Assert.That(analysed.Layout.Conditions.OfType<QuirkApplied>().Select(c => c.Quirk), Is.EqualTo(new[] { TagQuirk.PlainFrameSizes }));
        Assert.That(analysed.Source(TagFormat.Id3v2).State, Is.EqualTo(TagState.Intact));
        AssertOthersReadable(analysed, TagFormat.Id3v2);
        Assert.That(analysed.Text(analysed.Field(TagFormat.Id3v2, "COMM"))!.Single(), Has.Length.EqualTo(400 - 5).Or.Length.GreaterThan(100));
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    [TestCase("mp3/rw-illegal-frame-id.mp3")]
    [TestCase("mp3/dmg-id3v24-bad-frame-id.mp3")]
    public void AnIllegalFrameIdentifierWithAGoodSize_IsSteppedOver(string file)
    {
        var analysed = AudioCorpus.Analyse(file);

        Assert.That(analysed.Layout.Conditions.OfType<QuirkApplied>().Select(c => c.Quirk), Does.Contain(TagQuirk.IllegalFrameIdentifierSteppedOver));
        Assert.That(analysed.Source(TagFormat.Id3v2).State, Is.EqualTo(TagState.Intact));
        AssertOthersReadable(analysed, TagFormat.Id3v2);
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    [Test]
    public void Utf16WithoutAByteOrderMark_IsReadAsLittleEndian()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v24-utf16-no-bom.mp3");

        var text = analysed.Values.Text(analysed.Field(TagFormat.Id3v2, "TIT2"));
        Assert.That(text, Is.InstanceOf<FieldText.Readable>());
        Assert.That(((FieldText.Readable)text).Quirks, Is.EqualTo(TextQuirks.Utf16WithoutByteOrderMark));
    }

    [Test]
    public void TextInALocalCodePage_DeclaredLatin1_ReadsAsTheWrongText_ButUsable()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v23-codepage-as-latin1.mp3");

        Assert.That(analysed.Values.Text(analysed.Field(TagFormat.Id3v2, "TPE1")), Is.InstanceOf<FieldText.Readable>());
    }

    [Test]
    public void RandomBytesAfterTag_ReadAsAnId3v1TagOfWhateverTheySpell()
    {
        var analysed = AudioCorpus.Analyse("mp3/dmg-id3v1-garbage.mp3");

        AssertOthersReadable(analysed, TagFormat.Id3v1);
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    [TestCase("mp3/id3v24-junk-before-audio.mp3", 1000)]
    public void JunkBetweenTheTagAndTheAudio_IsReported_NotDamage(string file, int length)
    {
        var analysed = AudioCorpus.Analyse(file);

        Assert.That(analysed.Layout.Conditions.OfType<JunkBeforeAudio>().Single().Region.Length, Is.EqualTo(length));
        Assert.That(analysed.Layout.IsIncomplete, Is.False);
    }

    /// <summary>Every field of the tag but <paramref name="except"/> gives its content and, where it holds text, its text.</summary>
    private static void AssertOthersReadable(Analysed analysed, TagFormat format, FieldEntry? except = null)
    {
        foreach (var field in analysed.Source(format).Fields.Where(f => !ReferenceEquals(f, except)))
        {
            Assert.That(analysed.Values.Bytes(field), Is.InstanceOf<FieldContent.Readable>(), $"{field.Key}: bytes()");
            Assert.That(analysed.Values.Text(field), Is.Not.InstanceOf<FieldText.Unreadable>(), $"{field.Key}: field()");
        }
    }

    private static void AssertIncomplete(Analysed analysed, StructureBreak reason)
    {
        Assert.That(analysed.Layout.Conditions.OfType<TagStructureBroken>().Select(c => c.Reason), Is.EqualTo(new[] { reason }));
        Assert.That(analysed.Layout.IsIncomplete, Is.True);
    }
}
