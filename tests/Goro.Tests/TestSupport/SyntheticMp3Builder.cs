namespace Goro.Tests.TestSupport;

/// <summary>
/// Builds minimal-but-structurally-valid MP3 byte streams for tests, so tests don't
/// depend on a real audio fixture file. Frames carry no real encoded audio (their
/// body is arbitrary bytes) — Goro's reader only needs a valid frame header sequence
/// to locate the audio range; it doesn't decode the audio itself.
/// </summary>
internal static class SyntheticMp3Builder
{
    // MPEG1 Layer III, 128kbps, 44100Hz, mono, no CRC.
    private static readonly byte[] FrameHeader = [0xFF, 0xFB, 0x90, 0xC0];

    // 144 * bitrate / sampleRate = 144 * 128000 / 44100, no padding.
    private const int FrameLength = 417;

    public static byte[] BuildAudioFrames(int frameCount = 20)
    {
        var frame = new byte[FrameLength];
        FrameHeader.CopyTo(frame, 0);

        var audio = new byte[FrameLength * frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frame.CopyTo(audio, i * FrameLength);
        }

        return audio;
    }

    public static byte[] BuildId3V2(int bodySize, byte fill = 0)
    {
        var tag = new byte[10 + bodySize];
        tag[0] = (byte)'I';
        tag[1] = (byte)'D';
        tag[2] = (byte)'3';
        tag[3] = 3; // version 2.3
        tag[4] = 0;
        tag[5] = 0; // flags
        Synchsafe(bodySize).CopyTo(tag, 6);
        for (var i = 10; i < tag.Length; i++)
        {
            tag[i] = fill;
        }

        return tag;
    }

    public static byte[] BuildId3V1()
    {
        var tag = new byte[128];
        tag[0] = (byte)'T';
        tag[1] = (byte)'A';
        tag[2] = (byte)'G';
        return tag;
    }

    public static byte[] BuildMp3(byte[] audio, byte[]? id3v2 = null, byte[]? id3v1 = null)
    {
        using var stream = new MemoryStream();
        if (id3v2 is not null)
        {
            stream.Write(id3v2);
        }

        stream.Write(audio);
        if (id3v1 is not null)
        {
            stream.Write(id3v1);
        }

        return stream.ToArray();
    }

    private static byte[] Synchsafe(int value) =>
    [
        (byte)((value >> 21) & 0x7F),
        (byte)((value >> 14) & 0x7F),
        (byte)((value >> 7) & 0x7F),
        (byte)(value & 0x7F),
    ];
}
