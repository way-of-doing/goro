using Goro.Reading.Flac;
using Goro.Tests.TestSupport;
using static Goro.Tests.TestSupport.SyntheticFlacBuilder;

namespace Goro.Tests.Reading;

/// <summary>
/// A FLAC frame records no length, so where the last one ends comes from walking its body to the
/// bit (docs/implementation.md, "Finding where a FLAC file's audio ends"). The walk must land
/// exactly on every real frame's end, from the encoder's own output, and on every shape the
/// encoder never makes, built by hand.
/// </summary>
public class FlacFrameBodyTests
{
    private static IEnumerable<TestCaseData> FilesWithAChain() =>
        AudioCorpus.FlacFiles
            .Where(file => AudioCorpus.FlacMetadataOf(file).Metadata.End is not null)
            .Select(file => new TestCaseData(file).SetName($"frames: {file}"));

    /// <summary>
    /// Every frame of a healthy stream, walked by its body alone, ends exactly where the next frame's
    /// header starts, with its CRC-16 passing, until the audio ends.
    /// </summary>
    [TestCase("seeds/flac.flac", 36)]
    [TestCase("seeds/flac-minimal.flac", 36)]
    [TestCase("seeds/flac-piped.flac", 36)]
    [TestCase("flac/comments.flac", 36)]
    [TestCase("flac/two-comment-blocks.flac", 36)]
    public void EveryFrame_EndsWhereTheNextStarts(string file, int frames)
    {
        var bytes = AudioCorpus.Bytes(file);
        var (metadata, _) = AudioCorpus.FlacMetadataOf(file);
        var at = (int)metadata.End!.Value;
        var count = 0;
        while (at < bytes.Length)
        {
            Assert.That(FlacFrameHeader.TryParse(bytes.AsSpan(at), out var header), Is.True, $"a header at {at}");
            Assert.That(header.Number, Is.EqualTo((ulong)count), $"frame number at {at}");
            var end = FlacFrameBody.FindEnd(bytes.AsSpan(at), header, metadata.StreamInfo!.BitDepth);
            Assert.That(end, Is.InstanceOf<FlacFrameEnd.EndsAt>(), $"frame {count}");
            var endsAt = (FlacFrameEnd.EndsAt)end;
            Assert.That(endsAt.CrcPasses, Is.True, $"frame {count}'s CRC-16");
            at += endsAt.Length;
            count++;
        }

        Assert.That(at, Is.EqualTo(bytes.Length));
        Assert.That(count, Is.EqualTo(frames));
    }

    /// <summary>Wherever a corpus file's frames can be walked from its metadata, the walk finds the frames' own sequence.</summary>
    [TestCaseSource(nameof(FilesWithAChain))]
    public void TheFirstFrames_OfEveryFileWithAChain_EndWhereTheNextStarts(string file)
    {
        var bytes = AudioCorpus.Bytes(file);
        var (metadata, _) = AudioCorpus.FlacMetadataOf(file);
        var at = (int)metadata.End!.Value;
        for (var n = 0; n < 8; n++)
        {
            Assert.That(FlacFrameHeader.TryParse(bytes.AsSpan(at), out var header), Is.True, $"frame {n}");
            var end = (FlacFrameEnd.EndsAt)FlacFrameBody.FindEnd(bytes.AsSpan(at), header, 16);
            Assert.That(end.CrcPasses, Is.True, $"frame {n}");
            at += end.Length;
        }
    }

    [Test]
    public void AFrameCutOffByTheEndOfTheFile_RunsPast()
    {
        var bytes = AudioCorpus.Bytes("flac/truncated.flac");
        const int lastHeader = 26_868;

        Assert.That(FlacFrameHeader.TryParse(bytes.AsSpan(lastHeader), out var header), Is.True);
        Assert.That(header.Number, Is.EqualTo(19UL));
        Assert.That(FlacFrameBody.FindEnd(bytes.AsSpan(lastHeader), header, 16), Is.InstanceOf<FlacFrameEnd.RunsPast>());
    }

    [Test]
    public void AFrameFollowedByJunk_EndsWhereItsBodyDoes()
    {
        var frame = Frame(0, 64, 0, 4, variable: false, Verbatim());
        var withJunk = frame.Concat(Enumerable.Repeat((byte)0x5A, 300)).ToArray();

        Assert.That(FlacFrameHeader.TryParse(withJunk, out var header), Is.True);
        Assert.That(FlacFrameBody.FindEnd(withJunk, header, 0), Is.EqualTo(new FlacFrameEnd.EndsAt(frame.Length, true)));
    }

    [Test]
    public void ADamagedBody_FailsItsCrcOrIsMalformed()
    {
        var bytes = AudioCorpus.Bytes("seeds/flac.flac");
        var (metadata, _) = AudioCorpus.FlacMetadataOf("seeds/flac.flac");
        var at = (int)metadata.End!.Value;
        FlacFrameHeader.TryParse(bytes.AsSpan(at), out var header);
        var length = ((FlacFrameEnd.EndsAt)FlacFrameBody.FindEnd(bytes.AsSpan(at), header, 16)).Length;
        var damaged = bytes.AsSpan(at, length).ToArray();
        damaged[length / 2] ^= 0x10;

        var end = FlacFrameBody.FindEnd(damaged, header, 16);
        Assert.That(end is FlacFrameEnd.Malformed or FlacFrameEnd.RunsPast or FlacFrameEnd.EndsAt { CrcPasses: false }, Is.True, end.ToString());
    }

    private static IEnumerable<TestCaseData> Shapes()
    {
        yield return new TestCaseData(Frame(0, 4096, 0, 4, false, Constant(-5))).SetName("a constant subframe");
        yield return new TestCaseData(Frame(0, 100, 0, 4, false, Verbatim())).SetName("a verbatim subframe");
        yield return new TestCaseData(Frame(0, 100, 0, 4, false, VerbatimWithWastedBits(3))).SetName("wasted bits");
        yield return new TestCaseData(Frame(0, 100, 0, 6, false, VerbatimWithWastedBits(20))).SetName("wasted bits, many");
        yield return new TestCaseData(Frame(0, 100, 7, 4, false, Constant(), Verbatim(), Constant(), Verbatim(), Constant(), Verbatim(), Constant(), Verbatim())).SetName("eight channels");
        yield return new TestCaseData(Frame(0, 100, 8, 4, false, Verbatim(), Verbatim())).SetName("left/side, the side channel a bit wider");
        yield return new TestCaseData(Frame(0, 100, 9, 4, false, Verbatim(), Verbatim())).SetName("side/right");
        yield return new TestCaseData(Frame(0, 100, 10, 4, false, Verbatim(), Verbatim())).SetName("mid/side");
        yield return new TestCaseData(Frame(0, 100, 0, 4, false, FixedEscaped(2, 7))).SetName("an escaped partition");
        yield return new TestCaseData(Frame(0, 100, 0, 4, false, FixedEscaped(4, 0))).SetName("an escaped partition of zero-width samples");
        yield return new TestCaseData(Frame(0, 128, 0, 4, false, FixedRice(3, 4))).SetName("Rice-coded partitions");
        yield return new TestCaseData(Frame(5, 192, 0, 7, true, Verbatim())).SetName("32-bit samples");
    }

    [TestCaseSource(nameof(Shapes))]
    public void AFrameBuiltByHand_EndsWhereItWasBuiltToEnd(byte[] frame)
    {
        Assert.That(FlacFrameHeader.TryParse(frame, out var header), Is.True);
        Assert.That(FlacFrameBody.FindEnd(frame, header, 0), Is.EqualTo(new FlacFrameEnd.EndsAt(frame.Length, true)));
    }

    [Test]
    public void TheSideChannelsExtraBit_IsCounted()
    {
        // Built as independent channels but labelled left/side, the second channel is a bit short.
        var independent = Frame(0, 100, 1, 4, false, Verbatim(), Verbatim());
        var relabelled = (byte[])independent.Clone();
        relabelled[3] = (byte)((8 << 4) | (4 << 1));
        relabelled[7] = Crc8(relabelled[..7]);

        Assert.That(FlacFrameHeader.TryParse(relabelled, out var header), Is.True);
        Assert.That(FlacFrameBody.FindEnd(relabelled, header, 0), Is.Not.EqualTo(new FlacFrameEnd.EndsAt(independent.Length, true)));
    }

    [Test]
    public void AForbiddenPredictorPrecision_IsMalformed() =>
        Assert.That(FindEnd(Frame(0, 100, 0, 4, false, LpcWithForbiddenPrecision())), Is.InstanceOf<FlacFrameEnd.Malformed>());

    [Test]
    public void AReservedSubframeType_IsMalformed() =>
        Assert.That(FindEnd(Frame(0, 100, 0, 4, false, Reserved())), Is.InstanceOf<FlacFrameEnd.Malformed>());

    [Test]
    public void ABitDepthDeferredToAStreamInfoThatIsNotThere_IsMalformed()
    {
        var frame = Frame(0, 100, 0, 0, false, Constant());
        Assert.That(FlacFrameHeader.TryParse(frame, out var header), Is.True);
        Assert.That(FlacFrameBody.FindEnd(frame, header, 0), Is.InstanceOf<FlacFrameEnd.Malformed>());
        Assert.That(FlacFrameBody.FindEnd(frame, header, 16), Is.EqualTo(new FlacFrameEnd.EndsAt(frame.Length, true)));
    }

    [Test]
    public void AFrameOfZeroesAfterItsHeader_NeverRunsAway()
    {
        // A long run of zeros is a unary code that never ends; the walk must stop at the bytes' end.
        var frame = Frame(0, 4096, 0, 4, false, FixedRice(0, 0))[..20].Concat(new byte[1 << 16]).ToArray();
        Assert.That(FindEnd(frame), Is.InstanceOf<FlacFrameEnd.RunsPast>());
    }

    private static FlacFrameEnd FindEnd(byte[] frame)
    {
        Assert.That(FlacFrameHeader.TryParse(frame, out var header), Is.True);
        return FlacFrameBody.FindEnd(frame, header, 0);
    }
}
