using System.Buffers.Binary;

namespace Goro.Reading;

/// <summary>
/// The integers the formats store, Id3v2's syncsafe integers among them, and undoing Id3v2's
/// unsynchronisation. Every method reads from the start of the span it is given.
/// </summary>
internal static class Binary
{
    public static uint U32BigEndian(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt32BigEndian(bytes);

    public static uint U24BigEndian(ReadOnlySpan<byte> bytes) => (uint)(bytes[0] << 16 | bytes[1] << 8 | bytes[2]);

    public static ushort U16BigEndian(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt16BigEndian(bytes);

    public static uint U32LittleEndian(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);

    /// <summary>Whether four bytes hold a syncsafe integer: seven bits in each, the top bit clear.</summary>
    public static bool IsSyncsafe(ReadOnlySpan<byte> bytes) => ((bytes[0] | bytes[1] | bytes[2] | bytes[3]) & 0x80) == 0;

    public static uint Syncsafe(ReadOnlySpan<byte> bytes) =>
        (uint)((bytes[0] & 0x7F) << 21 | (bytes[1] & 0x7F) << 14 | (bytes[2] & 0x7F) << 7 | (bytes[3] & 0x7F));

    /// <summary>Undoes unsynchronisation: every <c>FF 00</c> becomes <c>FF</c>.</summary>
    public static byte[] Resynchronise(ReadOnlySpan<byte> data)
    {
        var result = new byte[data.Length];
        var length = 0;
        for (var i = 0; i < data.Length; i++)
        {
            result[length++] = data[i];
            if (data[i] == 0xFF && i + 1 < data.Length && data[i + 1] == 0x00)
            {
                i++;
            }
        }

        return result[..length];
    }
}
