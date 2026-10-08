using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

public class MpegFramesTests
{
    [Test]
    public void AHeader_GivesItsStreamParametersAndFrameLength()
    {
        Assert.That(MpegFrameHeader.TryParse([0xFF, 0xFB, 0x90, 0xC0], out var header), Is.True);

        Assert.That(header, Has.Property(nameof(MpegFrameHeader.Version)).EqualTo(1)
            .And.Property(nameof(MpegFrameHeader.Layer)).EqualTo(3)
            .And.Property(nameof(MpegFrameHeader.BitrateKbps)).EqualTo(128)
            .And.Property(nameof(MpegFrameHeader.SampleRate)).EqualTo(44100)
            .And.Property(nameof(MpegFrameHeader.FrameLength)).EqualTo(417));
    }

    [TestCase(new byte[] { 0xFF, 0xFB, 0xF0, 0xC0 }, TestName = "bitrate index 15 is not a header")]
    [TestCase(new byte[] { 0xFF, 0xFB, 0x9C, 0xC0 }, TestName = "sample rate index 3 is not a header")]
    [TestCase(new byte[] { 0xFF, 0xF9, 0x90, 0xC0 }, TestName = "layer bits 00 are not a header")]
    [TestCase(new byte[] { 0xFF, 0xEB, 0x90, 0xC0 }, TestName = "version bits 01 are not a header")]
    [TestCase(new byte[] { 0xFE, 0xFB, 0x90, 0xC0 }, TestName = "no sync is not a header")]
    [TestCase(new byte[] { 0xFF, 0xFB, 0x90 }, TestName = "three bytes are not a header")]
    public void WhatIsNotAHeader_DoesNotParse(byte[] bytes)
    {
        Assert.That(MpegFrameHeader.TryParse(bytes, out _), Is.False);
    }

    [Test]
    public void AFrame_IsConfirmedOnlyByAMatchingFrameWhereItEnds()
    {
        var two = SyntheticMp3Builder.BuildAudioFrames(2);
        var one = SyntheticMp3Builder.BuildAudioFrames(1);

        Assert.That(MpegFrames.IsConfirmed(Reader([.. two]), 0, two.Length, out _), Is.True);
        Assert.That(MpegFrames.IsConfirmed(Reader([.. one, .. new byte[500]]), 0, one.Length + 500, out _), Is.False, "nothing where it ends");
        Assert.That(MpegFrames.IsConfirmed(Reader([.. one]), 0, one.Length, out _), Is.False, "nothing after it at all");
    }

    // A free-format frame's length is only the distance to the next sync, which junk can fake: two
    // free-format headers some distance apart are not enough, a third at the same distance is.
    [Test]
    public void AFreeFormatFrame_NeedsAThirdFrameAtTheSameDistance()
    {
        byte[] free = [0xFF, 0xFB, 0x00, 0xC0];
        byte[] Spaced(int count) => [.. Enumerable.Range(0, count).SelectMany(_ => (byte[])[.. free, .. new byte[296]]), .. new byte[400]];

        Assert.That(MpegFrames.FindFirst(Reader(Spaced(2)), 0, Spaced(2).Length, out _), Is.Null);
        Assert.That(MpegFrames.FindFirst(Reader(Spaced(3)), 0, Spaced(3).Length, out _), Is.EqualTo(0));
    }

    [Test]
    public void TheSearch_GivesUpAtItsBudget_AndSaysSo()
    {
        byte[] file = [.. new byte[1000], .. SyntheticMp3Builder.BuildAudioFrames(3)];
        var reader = new BoundedReader(new MemoryByteSource(file), new ReadPolicy(HeadWindow: 16, TailWindow: 16, SearchBudget: 500));

        Assert.That(MpegFrames.FindFirst(reader, 0, file.Length, out var gaveUp), Is.Null);
        Assert.That(gaveUp, Is.True);
    }

    private static BoundedReader Reader(byte[] bytes) => new(new MemoryByteSource(bytes), ReadPolicy.Default);
}
