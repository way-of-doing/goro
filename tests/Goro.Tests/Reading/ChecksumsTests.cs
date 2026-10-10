using System.Text;
using Goro.Reading;
using Goro.Reading.Flac;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Reading;

/// <summary>
/// The checksums the reader trusts to tell a frame from chance bytes. Each is held to its published
/// check value, the CRC of the ASCII digits 1 to 9 (as the CRC catalogues give them: CRC-8/SMBUS,
/// CRC-16/UMTS, CRC-16/ARC), and the table-driven ones to a bit-at-a-time reference written here,
/// over every single byte, which pins every entry of their tables, and over longer data.
/// </summary>
public class ChecksumsTests
{
    private static readonly byte[] CheckInput = Encoding.ASCII.GetBytes("123456789");

    [Test]
    public void EachChecksum_GivesItsPublishedCheckValue()
    {
        Assert.That(Checksums.Crc8(CheckInput), Is.EqualTo(0xF4));
        Assert.That(Checksums.Crc16(CheckInput), Is.EqualTo(0xFEE8));
        Assert.That(Checksums.Crc16Arc(CheckInput), Is.EqualTo(0xBB3D));
    }

    [Test]
    public void OfNothing_EachChecksumIsZero()
    {
        Assert.That(Checksums.Crc8([]), Is.Zero);
        Assert.That(Checksums.Crc16([]), Is.Zero);
        Assert.That(Checksums.Crc16Arc([]), Is.Zero);
    }

    [Test]
    public void OfEverySingleByte_TheTablesAgreeWithTheBitwiseReference()
    {
        for (var b = 0; b < 256; b++)
        {
            byte[] input = [(byte)b];
            Assert.That(Checksums.Crc8(input), Is.EqualTo(SyntheticFlacBuilder.Crc8(input)), $"CRC-8 of {b:X2}");
            Assert.That(Checksums.Crc16(input), Is.EqualTo(SyntheticFlacBuilder.Crc16(input)), $"CRC-16 of {b:X2}");
        }
    }

    [Test]
    public void OfLongerData_TheTablesAgreeWithTheBitwiseReference()
    {
        var random = new Random(9639);
        for (var run = 0; run < 200; run++)
        {
            var input = new byte[random.Next(1, 5000)];
            random.NextBytes(input);
            Assert.That(Checksums.Crc8(input), Is.EqualTo(SyntheticFlacBuilder.Crc8(input)), $"run {run}");
            Assert.That(Checksums.Crc16(input), Is.EqualTo(SyntheticFlacBuilder.Crc16(input)), $"run {run}");
        }
    }

    /// <summary>A CRC run over data with its own CRC appended leaves no remainder, which is how a real frame checks out.</summary>
    [Test]
    public void ARealFlacFrame_ChecksOut()
    {
        var bytes = AudioCorpus.Bytes("seeds/flac.flac");
        var (metadata, _) = AudioCorpus.FlacMetadataOf("seeds/flac.flac");
        var at = (int)metadata.End!.Value;
        Assert.That(FlacFrameHeader.TryParse(bytes.AsSpan(at), out var header), Is.True);
        var length = ((FlacFrameEnd.EndsAt)FlacFrameBody.FindEnd(bytes.AsSpan(at), header, 16)).Length;

        Assert.That(Checksums.Crc8(bytes.AsSpan(at, header.Length)), Is.Zero, "the header with its CRC-8");
        Assert.That(Checksums.Crc16(bytes.AsSpan(at, length)), Is.Zero, "the frame with its CRC-16");
        Assert.That(Checksums.Crc16(bytes.AsSpan(at, length - 2)), Is.EqualTo(bytes[at + length - 2] << 8 | bytes[at + length - 1]));
    }
}
