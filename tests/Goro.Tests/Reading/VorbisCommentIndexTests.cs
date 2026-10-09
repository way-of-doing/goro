using System.Text;
using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Flac;
using Goro.Reading.Tags;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// The Vorbis comments of a FLAC file, and what damaged comments resolve to (docs/testing.md, the
/// FLAC table; docs/features/builtins/identifiers.md, "Source vorbis"): a comment without <c>=</c>
/// is absent, a value that does not decode is unusable through <c>field()</c> and intact through
/// <c>bytes()</c>, a comment running past its block breaks the list off there, and bytes after the
/// last comment mean the list was damaged.
/// </summary>
public class VorbisCommentIndexTests
{
    private static (TagRead Read, TagValues Values) Comments(string file, int which = 0)
    {
        var (metadata, reader) = AudioCorpus.FlacMetadataOf(file);
        var block = metadata.Blocks.Where(b => b.Type == FlacBlockType.VorbisComment).ElementAt(which);
        return (VorbisCommentIndex.Index(block.Comments!, block.Payload), new TagValues(reader));
    }

    private static IReadOnlyList<string>? Text(TagValues values, FieldEntry field) =>
        values.Text(field) is FieldText.Readable(var text, _) ? text : null;

    [Test]
    public void TheStandardComments_AreEachOneField_UnderTheirNamesAsRecorded()
    {
        var (read, values) = Comments("flac/comments.flac");

        Assert.That(read.Tag.State, Is.EqualTo(TagState.Intact));
        Assert.That(read.Conditions, Is.Empty);
        Assert.That(read.Tag.Fields.Select(f => f.Key), Is.EqualTo(new[] { "TITLE", "ARTIST", "ARTIST", "ALBUM", "DATE", "TRACKNUMBER", "GENRE", "mood", "COMMENT", "EMPTY" }));
        Assert.That(read.Tag.Fields.Select(f => Text(values, f)?.Single()), Is.EqualTo(new[] { "Title ✓", "AC/DC", "Motörhead", "Album", "1991-01-02", "3", "Rock", "calm", "  padded  ", "" }));
    }

    [Test]
    public void DamagedValues_AreUnusableOrAbsentAsEachDeserves()
    {
        var (read, values) = Comments("flac/dmg-comment-values.flac");

        Assert.That(read.Tag.Fields.Select(f => f.Key), Is.EqualTo(new[] { "TITLE", "ARTIST", "BAD~KEY" }), "NOEQUALSSIGN names nothing");
        Assert.That(read.Conditions.OfType<CommentWithoutName>().Count(), Is.EqualTo(1));
        Assert.That(read.Conditions.Any(c => c.MakesIncomplete), Is.False);

        var artist = read.Tag.Fields[1];
        Assert.That(values.Text(artist), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.DoesNotDecode)));
        Assert.That(values.Bytes(artist) is FieldContent.Readable(var bytes) && bytes.Span.SequenceEqual(Encoding.Latin1.GetBytes("Motörhead")), Is.True);
        Assert.That(Text(values, read.Tag.Fields[2]), Is.EqualTo(new[] { "x" }), "RFC 9639 allows ~ in a name");
    }

    [Test]
    public void ACommentRunningPastItsBlock_IsUnusable_AndBreaksTheListOff()
    {
        var (read, values) = Comments("flac/dmg-comment-length-overrun.flac");

        Assert.That(read.Tag.State, Is.EqualTo(TagState.BrokenOff));
        Assert.That(read.Tag.Fields.Select(f => f.Key), Is.EqualTo(new[] { "TITLE", "ARTIST", "ARTIST" }));
        Assert.That(read.Tag.Fields[2].RunsPastTag, Is.True);
        Assert.That(values.Text(read.Tag.Fields[2]), Is.EqualTo(new FieldText.Unreadable(UnreadableReason.RunsPastTag)));
        Assert.That(read.Conditions.OfType<TagStructureBroken>().Single().Reason, Is.EqualTo(StructureBreak.FieldRunsPastTag));
        Assert.That(read.Conditions.Any(c => c.MakesIncomplete), Is.True);
    }

    [Test]
    public void ZeroedComments_AreAbsent_AndTheBytesLeftAfterTheListBreakItOff()
    {
        var (read, values) = Comments("flac/dmg-zeroed-comment.flac");

        Assert.That(read.Tag.Fields.Select(f => f.Key), Is.EqualTo(new[] { "TITLE" }));
        Assert.That(Text(values, read.Tag.Fields[0]), Is.EqualTo(new[] { "Tit\0\0\0\0\0\0" }), "valid UTF-8, and one value however many NULs it holds");
        Assert.That(read.Conditions.OfType<CommentWithoutName>().Count(), Is.EqualTo(9));
        var broken = read.Conditions.OfType<TagStructureBroken>().Single();
        Assert.That(broken.Reason, Is.EqualTo(StructureBreak.BytesAfterLastComment));
        Assert.That(broken.At, Is.EqualTo(68 + 74));
        Assert.That(read.Tag.State, Is.EqualTo(TagState.BrokenOff));
    }

    [Test]
    public void OfTwoBlocks_EachIsIndexedOnItsOwn()
    {
        Assert.That(Comments("flac/two-comment-blocks.flac", 0).Read.Tag.Fields.Count, Is.EqualTo(10));
        Assert.That(Comments("flac/two-comment-blocks.flac", 1).Read.Tag.Fields.Select(f => f.Key), Is.EqualTo(new[] { "ARTIST" }));
    }

    [Test]
    public void ABlockDeclaredTooShort_IsIndexedWhole()
    {
        var (read, _) = Comments("flac/rw-comment-block-too-short.flac");
        Assert.That(read.Tag.State, Is.EqualTo(TagState.Intact));
        Assert.That(read.Tag.Fields.Count, Is.EqualTo(6));
    }

    [Test]
    public void AVendorStringRunningPastItsBlock_MakesTheTagUnusable()
    {
        var (read, _) = Index(List(vendorLength: 1000));

        Assert.That(read.Tag.State, Is.EqualTo(TagState.Unusable));
        Assert.That(read.Conditions.OfType<TagUnusable>().Single().Reason, Is.EqualTo(TagUnusableReason.VendorRunsPastTag));
    }

    [Test]
    public void ANameThatDoesNotDecode_IsUnusableForEveryName()
    {
        var (read, _) = Index(List(comments: [[0xC3, 0x28, (byte)'=', (byte)'x'], "TITLE=t"u8.ToArray()]));

        Assert.That(read.Tag.Fields.Select(f => (f.Key, f.NameUnreadable)), Is.EqualTo(new[] { ("", true), ("TITLE", false) }));
    }

    [Test]
    public void ACountTooHigh_BreaksTheListOffAtTheBlocksEnd()
    {
        var (read, _) = Index(List(comments: ["A=1"u8.ToArray(), "B=2"u8.ToArray()], count: 40));

        Assert.That(read.Tag.Fields.Select(f => f.Key), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(read.Conditions.OfType<TagStructureBroken>().Single().Reason, Is.EqualTo(StructureBreak.FieldRunsPastTag));
    }

    /// <summary>A comment list built by hand, as a FLAC block's payload: little-endian lengths, no framing bit.</summary>
    private static byte[] List(uint? vendorLength = null, byte[][]? comments = null, uint? count = null)
    {
        var vendor = "test"u8.ToArray();
        comments ??= [];
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(vendorLength ?? (uint)vendor.Length));
        bytes.AddRange(vendor);
        bytes.AddRange(BitConverter.GetBytes(count ?? (uint)comments.Length));
        foreach (var comment in comments)
        {
            bytes.AddRange(BitConverter.GetBytes((uint)comment.Length));
            bytes.AddRange(comment);
        }

        return [.. bytes];
    }

    private static (TagRead Read, TagValues Values) Index(byte[] payload)
    {
        var reader = new BoundedReader(new MemoryByteSource(payload), ReadPolicy.Default);
        var list = VorbisCommentIndex.Read(reader, 0, payload.Length);
        return (VorbisCommentIndex.Index(list, new Region(0, payload.Length)), new TagValues(reader));
    }
}
