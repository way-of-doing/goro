using Goro.Reading.Flac;
using Goro.Tests.TestSupport;
using static Goro.Tests.TestSupport.SyntheticFlacBuilder;

namespace Goro.Tests.Reading;

/// <summary>
/// A FLAC frame header (RFC 9639, section 9.1) is all that says a frame starts somewhere, so it must
/// be read exactly, and a header that is not one must be refused: its CRC-8, reserved and forbidden
/// codes, and the coded number, at every width.
/// </summary>
public class FlacFrameHeaderTests
{
    [Test]
    public void TheSeedsFirstFrame_IsReadInFull()
    {
        var (metadata, _) = AudioCorpus.FlacMetadataOf("seeds/flac.flac");
        var bytes = AudioCorpus.Bytes("seeds/flac.flac");

        Assert.That(FlacFrameHeader.TryParse(bytes.AsSpan((int)metadata.End!.Value), out var header), Is.True);
        Assert.That(header, Is.EqualTo(new FlacFrameHeader(false, 0, 4096, 44100, 1, FlacChannelAssignment.Independent, 16, 6)));
    }

    [TestCase(0UL)]
    [TestCase(0x7FUL)]
    [TestCase(0x80UL)]
    [TestCase(0x7FFUL)]
    [TestCase(0xFFFFUL)]
    [TestCase(0x1F_FFFFUL)]
    [TestCase(0x3FF_FFFFUL)]
    [TestCase(0x7FFF_FFFFUL)]
    [TestCase(0xF_FFFF_FFFFUL)]
    public void ACodedSampleNumber_IsReadAtEveryWidth(ulong number)
    {
        var frame = Frame(number, 4096, 0, 4, variable: true, Constant());

        Assert.That(FlacFrameHeader.TryParse(frame, out var header), Is.True);
        Assert.That(header.Number, Is.EqualTo(number));
        Assert.That(header.Length, Is.EqualTo(4 + CodedNumber(number).Length + 2 + 1));
    }

    [Test]
    public void AFrameNumberOfMoreThan31Bits_IsRefused()
    {
        Assert.That(FlacFrameHeader.TryParse(Frame(0x8000_0000UL, 4096, 0, 4, variable: false, Constant()), out _), Is.False);
        Assert.That(FlacFrameHeader.TryParse(Frame(0x7FFF_FFFFUL, 4096, 0, 4, variable: false, Constant()), out _), Is.True);
    }

    [TestCase(1)]
    [TestCase(192)]
    [TestCase(65535)]
    public void AnUncommonBlockSize_IsRead(int blockSize)
    {
        Assert.That(FlacFrameHeader.TryParse(Frame(0, blockSize, 0, 4, variable: false, Constant()), out var header), Is.True);
        Assert.That(header.BlockSize, Is.EqualTo(blockSize));
    }

    [Test]
    public void ABlockSizeOf65536_IsRefused()
    {
        // Stored as 65535, which RFC 9639 forbids: STREAMINFO could not record it.
        byte[] header = [0xFF, 0xF8, 0x79, 0x08, 0x00, 0xFF, 0xFF];
        Assert.That(FlacFrameHeader.TryParse([.. header, Crc8(header)], out _), Is.False);
    }

    [TestCase(0x0C, new byte[] { 48 }, 48000)]
    [TestCase(0x0D, new byte[] { 0xAC, 0x44 }, 44100)]
    [TestCase(0x0E, new byte[] { 0x27, 0x10 }, 100000)]
    [TestCase(0x00, new byte[0], 0)]
    public void AnUncommonSampleRate_IsRead(int code, byte[] stored, int rate)
    {
        byte[] header = [0xFF, 0xF8, (byte)(0xC0 | code), 0x08, 0x00, .. stored];
        Assert.That(FlacFrameHeader.TryParse([.. header, Crc8(header)], out var parsed), Is.True);
        Assert.That(parsed.SampleRate, Is.EqualTo(rate));
    }

    private static IEnumerable<TestCaseData> NotHeaders()
    {
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0xC9, 0x08, 0x00 }, true).SetName("a bad CRC-8");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF0, 0xC9, 0x08, 0x00 }, false).SetName("no sync code");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0x09, 0x08, 0x00 }, false).SetName("a reserved block size");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0xCF, 0x08, 0x00 }, false).SetName("sample rate bits 0b1111, forbidden");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0xC9, 0xB8, 0x00 }, false).SetName("a reserved channel assignment");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0xC9, 0x06, 0x00 }, false).SetName("a reserved bit depth");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0xC9, 0x09, 0x00 }, false).SetName("the reserved bit set");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0xC9, 0x08, 0xFF }, false).SetName("a coded number with an invalid first byte");
        yield return new TestCaseData(new byte[] { 0xFF, 0xF8, 0xC9, 0x08, 0xC2, 0x00 }, false).SetName("a coded number with an invalid continuation");
    }

    [TestCaseSource(nameof(NotHeaders))]
    public void AHeaderThatIsNotOne_IsRefused(byte[] header, bool corruptCrc)
    {
        var crc = (byte)(Crc8(header) ^ (corruptCrc ? 1 : 0));
        Assert.That(FlacFrameHeader.TryParse([.. header, crc], out _), Is.False);
    }

    [TestCase(8, FlacChannelAssignment.LeftSide)]
    [TestCase(9, FlacChannelAssignment.SideRight)]
    [TestCase(10, FlacChannelAssignment.MidSide)]
    [TestCase(5, FlacChannelAssignment.Independent)]
    public void TheChannelAssignment_IsRead(int code, FlacChannelAssignment assignment)
    {
        Assert.That(FlacFrameHeader.TryParse(Frame(0, 16, code, 4, variable: false), out var header), Is.True);
        Assert.That(header.Assignment, Is.EqualTo(assignment));
        Assert.That(header.Channels, Is.EqualTo(code >= 8 ? 2 : code + 1));
    }

    [Test]
    public void TheFirstAndEndSamples_FollowTheBlockingStrategy()
    {
        var fixedFrame = new FlacFrameHeader(false, 35, 2170, 44100, 1, FlacChannelAssignment.Independent, 16, 6);
        var variableFrame = fixedFrame with { VariableBlocking = true, Number = 143360 };

        Assert.That(fixedFrame.EndSample(4096), Is.EqualTo(145_530UL));
        Assert.That(variableFrame.EndSample(4096), Is.EqualTo(145_530UL));
    }
}
