using System.Collections.Frozen;

namespace Goro.Reading.Tags;

/// <summary>
/// Which identifier Goro reads an Id3v2 frame under: see "Frame names and tag versions" in
/// docs/features/builtins/identifiers.md.
/// </summary>
/// <remarks>
/// A frame is named by its v2.4 identifier wherever v2.4 has a frame holding the same data in the
/// same form, and by its own identifier otherwise. The name depends on the frame alone, never on
/// the revision of the tag it is found in, so the table below is keyed on the identifier as
/// recorded and nothing else.
/// </remarks>
public static class Id3v2FrameNames
{
    /// <summary>The frames read under another identifier: those of v2.2, and three of v2.3.</summary>
    public static FrozenDictionary<string, string> Renamed { get; } = new Dictionary<string, string>
    {
        // v2.3 frames that v2.4 replaced with one holding the same data in the same form.
        ["IPLS"] = "TIPL", ["TORY"] = "TDOR", ["TYER"] = "TDRC",

        // v2.2 frames, whose three-character identifiers v2.3 lengthened.
        ["BUF"] = "RBUF", ["CNT"] = "PCNT", ["COM"] = "COMM", ["CRA"] = "AENC", ["ETC"] = "ETCO",
        ["GEO"] = "GEOB", ["IPL"] = "TIPL", ["MCI"] = "MCDI", ["MLL"] = "MLLT", ["POP"] = "POPM",
        ["REV"] = "RVRB", ["SLT"] = "SYLT", ["STC"] = "SYTC", ["TAL"] = "TALB", ["TBP"] = "TBPM",
        ["TCM"] = "TCOM", ["TCO"] = "TCON", ["TCR"] = "TCOP", ["TDY"] = "TDLY", ["TEN"] = "TENC",
        ["TFT"] = "TFLT", ["TKE"] = "TKEY", ["TLA"] = "TLAN", ["TLE"] = "TLEN", ["TMT"] = "TMED",
        ["TOA"] = "TOPE", ["TOF"] = "TOFN", ["TOL"] = "TOLY", ["TOR"] = "TDOR", ["TOT"] = "TOAL",
        ["TP1"] = "TPE1", ["TP2"] = "TPE2", ["TP3"] = "TPE3", ["TP4"] = "TPE4", ["TPA"] = "TPOS",
        ["TPB"] = "TPUB", ["TRC"] = "TSRC", ["TRK"] = "TRCK", ["TSS"] = "TSSE", ["TT1"] = "TIT1",
        ["TT2"] = "TIT2", ["TT3"] = "TIT3", ["TXT"] = "TEXT", ["TXX"] = "TXXX", ["TYE"] = "TDRC",
        ["UFI"] = "UFID", ["ULT"] = "USLT", ["WAF"] = "WOAF", ["WAR"] = "WOAR", ["WAS"] = "WOAS",
        ["WCM"] = "WCOM", ["WCP"] = "WCOP", ["WPB"] = "WPUB", ["WXX"] = "WXXX",
    }.ToFrozenDictionary();

    /// <summary>
    /// The v2.2 frames with no counterpart of the same form, which keep their three-character
    /// identifiers. The v2.3 frames in the same position need no list, having four characters
    /// like any other.
    /// </summary>
    public static FrozenSet<string> KeptFromV22 { get; } =
        new[] { "CRM", "EQU", "LNK", "PIC", "RVA", "TDA", "TIM", "TRD", "TSI" }.ToFrozenSet();

    /// <summary>The identifier a frame recorded as <paramref name="recorded"/> is read under.</summary>
    public static string Canonical(string recorded) => Renamed.GetValueOrDefault(recorded, recorded);

    /// <summary>How a frame's content is laid out, by the identifier it is read under.</summary>
    public static FieldForm FormOf(string canonical) => canonical switch
    {
        "TXXX" => FieldForm.Id3v2DescribedText,
        "COMM" or "USLT" => FieldForm.Id3v2LanguageText,
        "WXXX" => FieldForm.Id3v2DescribedUrl,

        // Apple's iTunes frames, laid out as text frames although not named T... (docs/design/quirks.md).
        "GRP1" or "MVNM" or "MVIN" => FieldForm.Id3v2Text,
        _ when canonical[0] == 'T' => FieldForm.Id3v2Text,
        _ when canonical[0] == 'W' => FieldForm.Id3v2Url,
        _ => FieldForm.Id3v2Binary,
    };
}
