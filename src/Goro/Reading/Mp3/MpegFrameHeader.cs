namespace Goro.Reading.Mp3;

/// <summary>
/// An MPEG audio frame header: four bytes, starting with eleven set bits. <see cref="Version"/> is
/// 1, 2 or 25 (for 2.5). A free-format frame states no bitrate, and has a <see cref="FrameLength"/>
/// of 0 until the distance to the next frame says what it is.
/// </summary>
public readonly record struct MpegFrameHeader(
    int Version, int Layer, bool Crc, int BitrateKbps, int SampleRate, bool Padding, int ChannelMode, int FrameLength)
{
    public const int Length = 4;

    private static readonly int[][] Bitrates =
    [
        [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448], // V1 L1
        [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384],    // V1 L2
        [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320],     // V1 L3
        [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256],    // V2 L1
        [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160],         // V2 L2, L3
    ];

    public bool IsFreeFormat => BitrateKbps == 0;

    public int SamplesPerFrame => Layer switch { 1 => 384, 2 => 1152, _ => Version == 1 ? 1152 : 576 };

    /// <summary>Parses the first four bytes of <paramref name="bytes"/>.</summary>
    public static bool TryParse(ReadOnlySpan<byte> bytes, out MpegFrameHeader header)
    {
        header = default;
        if (bytes.Length < Length || bytes[0] != 0xFF || (bytes[1] & 0xE0) != 0xE0)
        {
            return false;
        }

        var versionBits = (bytes[1] >> 3) & 3;
        var layerBits = (bytes[1] >> 1) & 3;
        var bitrateIndex = bytes[2] >> 4;
        var rateIndex = (bytes[2] >> 2) & 3;
        if (versionBits == 1 || layerBits == 0 || bitrateIndex == 15 || rateIndex == 3)
        {
            return false;
        }

        var version = versionBits switch { 3 => 1, 2 => 2, _ => 25 };
        var layer = 4 - layerBits;
        int[] rates = version switch { 1 => [44100, 48000, 32000], 2 => [22050, 24000, 16000], _ => [11025, 12000, 8000] };
        var sampleRate = rates[rateIndex];
        var kbps = Bitrates[version == 1 ? layer - 1 : layer == 1 ? 3 : 4][bitrateIndex];
        var padding = ((bytes[2] >> 1) & 1) == 1;
        var frameLength = kbps == 0
            ? 0
            : layer == 1
                ? (12 * kbps * 1000 / sampleRate + (padding ? 1 : 0)) * 4
                : (layer == 3 && version != 1 ? 72 : 144) * kbps * 1000 / sampleRate + (padding ? 1 : 0);
        header = new MpegFrameHeader(version, layer, (bytes[1] & 1) == 0, kbps, sampleRate, padding, bytes[3] >> 6, frameLength);
        return true;
    }

    /// <summary>Whether another header belongs to the same stream: what confirming a frame checks.</summary>
    public bool Matches(MpegFrameHeader other) =>
        other.Version == Version && other.Layer == Layer && other.SampleRate == SampleRate;
}
