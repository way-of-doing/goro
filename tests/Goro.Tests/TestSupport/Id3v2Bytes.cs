using System.Text;

namespace Goro.Tests.TestSupport;

/// <summary>
/// Builds Id3v2 tags byte by byte, for the shapes the fixture corpus does not hold. Nothing is
/// written through a tag library, whose assumptions would be shared with the reader under test.
/// </summary>
internal static class Id3v2Bytes
{
    public sealed record Frame(string Id, byte[] Content, ushort Flags = 0);

    /// <summary>
    /// A tag of revision <paramref name="major"/> holding <paramref name="frames"/>, followed by
    /// <paramref name="padding"/> zero bytes. A v2.2 frame has a three-character identifier, a
    /// three-byte size and no flags.
    /// </summary>
    public static byte[] Tag(int major, IEnumerable<Frame> frames, int padding = 0, byte flags = 0)
    {
        using var body = new MemoryStream();
        foreach (var frame in frames)
        {
            body.Write(Encoding.ASCII.GetBytes(frame.Id));
            if (major == 2)
            {
                body.Write(BigEndian(frame.Content.Length)[1..]);
            }
            else
            {
                body.Write(major == 4 ? Syncsafe(frame.Content.Length) : BigEndian(frame.Content.Length));
                body.Write([(byte)(frame.Flags >> 8), (byte)frame.Flags]);
            }

            body.Write(frame.Content);
        }

        body.Write(new byte[padding]);
        return [.. "ID3"u8, (byte)major, 0, flags, .. Syncsafe((int)body.Length), .. body.ToArray()];
    }

    /// <summary>A tag, then <paramref name="frameCount"/> audio frames: enough for the analysis to find the audio.</summary>
    public static byte[] Mp3(byte[] tag, int frameCount = 20) => [.. tag, .. SyntheticMp3Builder.BuildAudioFrames(frameCount)];

    /// <summary>Content of a text frame: an encoding byte, then the bytes as given.</summary>
    public static byte[] Text(byte encoding, params byte[] text) => [encoding, .. text];

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>A text frame's content in UTF-8, its values separated by NUL.</summary>
    public static byte[] Utf8Text(params string[] values) => [3, .. Encoding.UTF8.GetBytes(string.Join('\0', values))];

    /// <summary>A text frame's content in ISO-8859-1, which every revision defines.</summary>
    public static byte[] Latin1Text(params string[] values) => [0, .. Encoding.Latin1.GetBytes(string.Join('\0', values))];

    /// <summary>The identifier a frame has in <paramref name="major"/>: its v2.2 name in v2.2, where it has one.</summary>
    public static string Named(int major, string frame) =>
        major == 2 ? Goro.Reading.Tags.Id3v2FrameNames.Renamed.Single(pair => pair.Value == frame && pair.Key.Length == 3).Key : frame;

    public static byte[] Syncsafe(int value) =>
        [(byte)((value >> 21) & 0x7F), (byte)((value >> 14) & 0x7F), (byte)((value >> 7) & 0x7F), (byte)(value & 0x7F)];

    private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
