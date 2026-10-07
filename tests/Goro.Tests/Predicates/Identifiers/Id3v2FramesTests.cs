using Goro.Predicates.Identifiers;

namespace Goro.Tests.Predicates.Identifiers;

/// <summary>
/// How the source functions of <c>id3v2</c> take a frame: by the identifier Goro reads it under,
/// which is its v2.4 identifier wherever v2.4 has a frame holding the same data in the same form.
/// </summary>
public class Id3v2FramesTests
{
    [TestCase("TIT2")]
    [TestCase("TXXX")]
    [TestCase("APIC")]
    [TestCase("XYZ1")]
    [TestCase("TDAT")]
    [TestCase("TIME")]
    [TestCase("RVAD")]
    [TestCase("EQUA")]
    public void AFourCharacterIdentifier_NotRenamed_IsItself(string frame)
    {
        Assert.That(Id3v2Frames.Canonical(frame), Is.EqualTo(frame));
    }

    [TestCase("tit2", "TIT2")]
    [TestCase("Apic", "APIC")]
    [TestCase("pic", "PIC")]
    public void AnIdentifier_IsMatchedWithoutRegardToCase(string written, string frame)
    {
        Assert.That(Id3v2Frames.Canonical(written), Is.EqualTo(frame));
    }

    // The v2.2 frames with no counterpart of the same form, and only those, keep three characters.
    [TestCase("CRM")]
    [TestCase("EQU")]
    [TestCase("LNK")]
    [TestCase("PIC")]
    [TestCase("RVA")]
    [TestCase("TDA")]
    [TestCase("TIM")]
    [TestCase("TRD")]
    [TestCase("TSI")]
    public void AV22FrameWithNoCounterpart_KeepsItsIdentifier(string frame)
    {
        Assert.That(Id3v2Frames.Canonical(frame), Is.EqualTo(frame));
    }

    [TestCase("TYER", "TDRC")]
    [TestCase("TORY", "TDOR")]
    [TestCase("IPLS", "TIPL")]
    [TestCase("TT2", "TIT2")]
    [TestCase("TYE", "TDRC")]
    [TestCase("TP1", "TPE1")]
    [TestCase("TCO", "TCON")]
    [TestCase("COM", "COMM")]
    [TestCase("ULT", "USLT")]
    [TestCase("TXX", "TXXX")]
    [TestCase("WXX", "WXXX")]
    [TestCase("POP", "POPM")]
    public void ARenamedFrame_IsRejected_OfferingItsV24Identifier(string written, string renamed)
    {
        Assert.That(Id3v2Frames.Canonical(written), Is.Null);
        Assert.That(Id3v2Frames.Renamed[written], Is.EqualTo(renamed));

        var check = Id3v2Frames.CheckBytes([written.ToLowerInvariant()]);

        Assert.That(check, Is.EqualTo(new ArgumentCheck.Rejected(0, new ArgumentRejection.FrameRenamed(written.ToLowerInvariant(), renamed))));
    }

    [Test]
    public void EveryRename_LeadsToAFrameThatIsItselfNotRenamed()
    {
        Assert.That(Id3v2Frames.Renamed.Values.Where(renamed => Id3v2Frames.Canonical(renamed) != renamed), Is.Empty);
    }

    [TestCase("")]
    [TestCase("TI")]
    [TestCase("ABC")]
    [TestCase("TIT22")]
    [TestCase("T-T2")]
    [TestCase("TÏT2")]
    public void AnythingElse_IsMalformed(string written)
    {
        Assert.That(Id3v2Frames.Canonical(written), Is.Null);
        Assert.That(Id3v2Frames.CheckBytes([written]), Is.EqualTo(new ArgumentCheck.Rejected(0, new ArgumentRejection.MalformedFrame(written))));
    }

    [TestCase("TIT2", true)]
    [TestCase("TXXX", true)]
    [TestCase("TDA", true)]
    [TestCase("WOAR", true)]
    [TestCase("WXXX", true)]
    [TestCase("COMM", true)]
    [TestCase("USLT", true)]
    [TestCase("APIC", false)]
    [TestCase("POPM", false)]
    [TestCase("PRIV", false)]
    [TestCase("PIC", false)]
    [TestCase("SYLT", false)]
    public void Field_ReadsOnlyFramesThatHoldText(string frame, bool holdsText)
    {
        var check = Id3v2Frames.CheckField([frame]);

        Assert.That(check, holdsText
            ? Is.InstanceOf<ArgumentCheck.Accepted>()
            : Is.EqualTo(new ArgumentCheck.Rejected(0, new ArgumentRejection.FrameNotText(frame))));
    }

    [TestCase("TXXX")]
    [TestCase("WXXX")]
    [TestCase("COMM")]
    [TestCase("USLT")]
    public void AFrameWithADescription_TakesOne(string frame)
    {
        var check = (ArgumentCheck.Accepted)Id3v2Frames.CheckField([frame.ToLowerInvariant(), "MOOD"]);

        Assert.That(check.Canonical, Is.EqualTo(new[] { frame, "MOOD" }));
    }

    [TestCase("TIT2")]
    [TestCase("WOAR")]
    public void AnyOtherFrame_TakesNoDescription(string frame)
    {
        Assert.That(Id3v2Frames.CheckField([frame, "MOOD"]),
            Is.EqualTo(new ArgumentCheck.Rejected(1, new ArgumentRejection.DescriptionNotTaken(frame))));
    }

    [Test]
    public void Bytes_ReadsAFrameOfAnyKind()
    {
        Assert.That(Id3v2Frames.CheckBytes(["apic"]), Is.InstanceOf<ArgumentCheck.Accepted>());
        Assert.That(((ArgumentCheck.Accepted)Id3v2Frames.CheckBytes(["tit2"])).Canonical, Is.EqualTo(new[] { "TIT2" }));
    }
}
