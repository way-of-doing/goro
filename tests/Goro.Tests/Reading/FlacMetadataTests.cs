using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Flac;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// A FLAC file's metadata block chain, and where it says the audio starts (<c>K</c> in
/// docs/design/flac-layout.md). Where a block's recorded length is wrong, the block's own contents
/// may give its end, and the end from which the rest of the chain reads cleanly wins
/// (docs/design/quirks.md). The positions expected are the ones flac-layout measured, by a probe
/// that trusts every length or searches for the first frame.
/// </summary>
public class FlacMetadataTests
{
    private static FlacMetadata Metadata(string file) => AudioCorpus.FlacMetadataOf(file).Metadata;

    [TestCase("seeds/flac.flac", 8304)]
    [TestCase("seeds/flac-minimal.flac", 86)]
    [TestCase("flac/comments.flac", 4496)]
    [TestCase("flac/comment-block-last.flac", 1259)]
    [TestCase("flac/two-comment-blocks.flac", 286)]
    [TestCase("flac/id3v2-prefix.flac", 4610)]
    [TestCase("flac/rw-picture-block-and-comment.flac", 1561)]
    [TestCase("flac/dmg-zeroed-comment.flac", 4496)]
    [TestCase("flac/dmg-comment-length-overrun.flac", 747)]
    public void AHealthyChain_EndsWhereTheFirstFrameStarts(string file, long k)
    {
        var metadata = Metadata(file);
        Assert.That(metadata.End, Is.EqualTo(k));
        Assert.That(metadata.Conditions.OfType<FlacQuirkApplied>(), Is.Empty);
        Assert.That(metadata.Conditions.OfType<MetadataChainLost>(), Is.Empty);
        Assert.That(metadata.Blocks.Count(block => block.Last), Is.EqualTo(1));
    }

    [Test]
    public void TheBlocks_AreListedInOrder()
    {
        var metadata = Metadata("flac/comments.flac");
        Assert.That(metadata.Blocks.Select(block => block.Type), Is.EqualTo(new[]
        {
            FlacBlockType.StreamInfo, FlacBlockType.SeekTable, FlacBlockType.VorbisComment,
            FlacBlockType.Application, FlacBlockType.Picture, FlacBlockType.Padding,
        }));
        Assert.That(metadata.Blocks.Select(block => block.At), Is.EqualTo(new long[] { 4, 42, 64, 253, 277, 396 }));
        Assert.That(metadata.Blocks[2].Comments, Is.Not.Null);
    }

    [Test]
    public void StreamInfo_IsRead()
    {
        var info = Metadata("flac/comments.flac").StreamInfo!;
        Assert.That((info.MinBlockSize, info.MaxBlockSize, info.SampleRate, info.Channels, info.BitDepth, info.TotalSamples),
            Is.EqualTo((4096, 4096, 44100, 1, 16, 145_530UL)));
        Assert.That(Metadata("flac/streaminfo-total-zero.flac").StreamInfo!.TotalSamples, Is.Zero);
        Assert.That(Metadata("flac/streaminfo-total-doubled.flac").StreamInfo!.TotalSamples, Is.EqualTo(291_060UL));
    }

    [Test]
    public void AShortStreamInfo_IsReadForTheFieldsItHolds()
    {
        var metadata = Metadata("flac/rw-streaminfo-short.flac");

        Assert.That(metadata.End, Is.EqualTo(1184), "its recorded 18 bytes are right: the chain reads on from there");
        Assert.That(metadata.StreamInfo!.TotalSamples, Is.EqualTo(145_530UL));
        Assert.That(metadata.StreamInfo.SampleRate, Is.EqualTo(44100));
        Assert.That(metadata.Conditions, Is.EquivalentTo(new Condition[] { new StreamInfoShort(18) }));
        Assert.That(metadata.Conditions.Any(c => c.MakesIncomplete), Is.False);
    }

    /// <summary>Contents that run on past their recorded length to a clean chain: the block is read whole, silently.</summary>
    [TestCase("flac/rw-comment-block-too-short.flac", 42, 193, FlacBlockType.VorbisComment, 48)]
    [TestCase("flac/rw-picture-block-length-zero.flac", 172, 1319, FlacBlockType.Picture, 0)]
    public void ABlockDeclaredTooShort_IsReadToTheEndItsContentsGive(string file, long blockAt, long k, FlacBlockType type, long recorded)
    {
        var metadata = Metadata(file);
        var block = metadata.Blocks.Single(b => b.At == blockAt);

        Assert.That(metadata.End, Is.EqualTo(k));
        Assert.That(block.Type, Is.EqualTo(type));
        Assert.That(block.RecordedLength, Is.EqualTo(recorded));
        Assert.That(block.LengthRecovered, Is.True);
        Assert.That(metadata.Conditions, Is.EquivalentTo(new Condition[] { new FlacQuirkApplied(FlacQuirk.BlockLengthRecovered, blockAt) }));
        Assert.That(metadata.Conditions.Any(c => c.MakesIncomplete), Is.False);
    }

    [Test]
    public void ABlockDeclaredTooLong_IsReadToTheEndItsContentsGive()
    {
        var metadata = Metadata("flac/dmg-block-length-overrun.flac");
        var comments = metadata.Blocks.Single(b => b.Type == FlacBlockType.VorbisComment);

        Assert.That(metadata.End, Is.EqualTo(1259));
        Assert.That(comments.RecordedLength, Is.EqualTo(385));
        Assert.That(comments.Payload.Length, Is.EqualTo(185));
        Assert.That(metadata.Blocks.Select(b => b.Type), Is.EqualTo(new[] { FlacBlockType.StreamInfo, FlacBlockType.VorbisComment, FlacBlockType.Padding }));
        Assert.That(metadata.Conditions, Is.EquivalentTo(new Condition[] { new FlacQuirkApplied(FlacQuirk.BlockLengthRecovered, 42) }));
    }

    [Test]
    public void ARecordedLengthThatReadsOnCleanly_Wins_OverContentsThatEndEarly()
    {
        var metadata = Metadata("flac/dmg-zeroed-comment.flac");
        var comments = metadata.Blocks.Single(b => b.Type == FlacBlockType.VorbisComment);

        Assert.That(comments.LengthRecovered, Is.False);
        Assert.That(comments.Comments!.End, Is.EqualTo(comments.Payload.Start + 74), "the list ends early, which is the list's damage, not the block's");
    }

    [Test]
    public void NoBlockFlaggedLast_TheChainEndsAtTheFrameItMeets()
    {
        var metadata = Metadata("flac/dmg-no-last-block-flag.flac");

        Assert.That(metadata.End, Is.EqualTo(491));
        Assert.That(metadata.Blocks.Any(b => b.Last), Is.False);
        Assert.That(metadata.Conditions, Is.EquivalentTo(new Condition[] { new FlacQuirkApplied(FlacQuirk.NoLastBlockFlag, 491) }));
    }

    [Test]
    public void AnOpaqueBlockDeclaredTooLong_LosesTheChain()
    {
        var metadata = Metadata("flac/dmg-application-length-overrun.flac");

        Assert.That(metadata.End, Is.Null);
        Assert.That(metadata.Blocks.Select(b => b.Type), Is.EqualTo(new[] { FlacBlockType.StreamInfo, FlacBlockType.VorbisComment, FlacBlockType.Application }),
            "the blocks up to the one that does not state its extent are read");
        Assert.That(metadata.Conditions.OfType<MetadataChainLost>().Single().MakesIncomplete, Is.True);
    }

    [Test]
    public void AFileEndingInsideItsMetadata_LosesTheChain()
    {
        var bytes = AudioCorpus.Bytes("flac/comments.flac")[..300];
        var metadata = FlacMetadata.Read(new BoundedReader(new MemoryByteSource(bytes), ReadPolicy.Default), 4);

        Assert.That(metadata.End, Is.Null);
        Assert.That(metadata.Conditions.OfType<MetadataChainLost>().Single().Reason, Is.EqualTo(MetadataChainBreak.BlockRunsPastFile));
    }

    [Test]
    public void AChainOfEmptyBlocks_IsCutOff()
    {
        // After a real STREAMINFO, zero bytes read as an empty second one: a chain read from the wrong place.
        var streamInfo = AudioCorpus.Bytes("seeds/flac.flac")[8..42];
        byte[] bytes = [.. "fLaC"u8.ToArray(), 0x00, 0x00, 0x00, 0x22, .. streamInfo, .. new byte[100_000]];
        var metadata = FlacMetadata.Read(new BoundedReader(new MemoryByteSource(bytes), ReadPolicy.Default), 4);

        Assert.That(metadata.End, Is.Null);
        Assert.That(metadata.Conditions.OfType<MetadataChainLost>().Single().Reason, Is.EqualTo(MetadataChainBreak.SecondStreamInfo));
    }
}
