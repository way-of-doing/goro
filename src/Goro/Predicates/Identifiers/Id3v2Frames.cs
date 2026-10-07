using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// What Goro knows about Id3v2 frame identifiers, and the checks the source functions of
/// <c>id3v2</c> make of their arguments: see "Frame names and tag versions" in
/// docs/features/builtins/identifiers.md.
/// </summary>
/// <remarks>
/// A frame is named by its v2.4 identifier wherever v2.4 has a frame holding the same data in the
/// same form, and by its own identifier otherwise. The name depends on the frame alone, never on
/// the revision of the tag it is found in, so the table below is keyed on the identifier as
/// recorded and nothing else.
/// </remarks>
public static class Id3v2Frames
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

    /// <summary>The frames told apart by a description, which <c>field()</c> can select by.</summary>
    public static FrozenSet<string> Described { get; } = new[] { "TXXX", "WXXX", "COMM", "USLT" }.ToFrozenSet();

    /// <summary>Whether the frame holds text that <c>field()</c> can read: a text or URL frame, a comment, or lyrics.</summary>
    public static bool HoldsText(string frame) => frame[0] is 'T' or 'W' || frame is "COMM" or "USLT";

    /// <summary>The arguments of <c>id3v2::field()</c>: a frame that holds text, and a description where it carries one.</summary>
    public static ArgumentCheck CheckField(ImmutableArray<string> arguments)
    {
        if (Canonical(arguments[0]) is not { } frame)
        {
            return Rejected(arguments[0]);
        }

        if (!HoldsText(frame))
        {
            return new ArgumentCheck.Rejected(0, new ArgumentRejection.FrameNotText(frame));
        }

        if (arguments.Length > 1 && !Described.Contains(frame))
        {
            return new ArgumentCheck.Rejected(1, new ArgumentRejection.DescriptionNotTaken(frame));
        }

        return new ArgumentCheck.Accepted(arguments.SetItem(0, frame));
    }

    /// <summary>The argument of <c>id3v2::bytes()</c>: any frame.</summary>
    public static ArgumentCheck CheckBytes(ImmutableArray<string> arguments) =>
        Canonical(arguments[0]) is { } frame
            ? new ArgumentCheck.Accepted([frame])
            : Rejected(arguments[0]);

    /// <summary>
    /// The identifier Goro reads a frame under, in upper case, or null where <paramref name="written"/>
    /// is not one: renamed, or not the shape of an identifier at all.
    /// </summary>
    public static string? Canonical(string written)
    {
        var frame = written.ToUpperInvariant();
        var shaped = frame.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c))
            && (frame.Length == 4 || frame.Length == 3 && KeptFromV22.Contains(frame));
        return shaped && !Renamed.ContainsKey(frame) ? frame : null;
    }

    private static ArgumentCheck.Rejected Rejected(string written) =>
        Renamed.TryGetValue(written.ToUpperInvariant(), out var renamed)
            ? new ArgumentCheck.Rejected(0, new ArgumentRejection.FrameRenamed(written, renamed))
            : new ArgumentCheck.Rejected(0, new ArgumentRejection.MalformedFrame(written));
}
