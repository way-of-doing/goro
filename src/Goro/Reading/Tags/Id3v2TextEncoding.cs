using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Goro.Reading.Tags;

/// <summary>
/// What the strings of one Id3v2 frame share as they are decoded one after another: the byte order
/// the first UTF-16 string with a byte order mark set, which a later one without a mark takes, and
/// what had to be assumed on the way. It belongs to reading one frame, never to an encoding.
/// </summary>
internal struct FrameTextState
{
    public bool? BigEndian;

    public TextQuirks Quirks;
}

/// <summary>
/// One of the text encodings an Id3v2 frame's first byte names. Text that is not valid in the
/// encoding is reported, never replaced with U+FFFD. Each encoding is a singleton with no state;
/// <see cref="FromByte"/> chooses one.
/// </summary>
internal interface IId3v2TextEncoding
{
    /// <summary>The width of a terminator: one byte, or two in UTF-16.</summary>
    int TerminatorLength { get; }

    /// <summary>Decodes one string, or null where it is not valid in the encoding.</summary>
    string? Decode(ReadOnlySpan<byte> bytes, ref FrameTextState state);

    /// <summary>Where the first terminator is, or -1. A UTF-16 terminator is two zero bytes on a character boundary.</summary>
    int FindTerminator(ReadOnlySpan<byte> bytes)
    {
        if (TerminatorLength == 1)
        {
            return bytes.IndexOf((byte)0);
        }

        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            if (bytes[i] == 0 && bytes[i + 1] == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Splits text into the values its terminators separate, as every revision is read
    /// (docs/design/quirks.md). A terminator after the last value ends it, and begins no further
    /// value. Null where any value does not decode.
    /// </summary>
    ImmutableArray<string>? DecodeValues(ReadOnlySpan<byte> bytes, ref FrameTextState state)
    {
        var values = ImmutableArray.CreateBuilder<string>();
        while (true)
        {
            var terminator = FindTerminator(bytes);
            if (Decode(terminator < 0 ? bytes : bytes[..terminator], ref state) is not { } value)
            {
                return null;
            }

            values.Add(value);
            if (terminator < 0)
            {
                break;
            }

            bytes = bytes[(terminator + TerminatorLength)..];
            if (bytes.IsEmpty)
            {
                break;
            }
        }

        return values.ToImmutable();
    }

    /// <summary>
    /// The encoding a frame's first byte names, as section 4 of the Id3v2.4 structure document defines
    /// them, or false for a byte no revision defines. v2.2 and v2.3 define only <c>$00</c> and
    /// <c>$01</c>; the other two are read in a tag of any revision (docs/design/quirks.md).
    /// </summary>
    static bool FromByte(byte value, [NotNullWhen(true)] out IId3v2TextEncoding? encoding)
    {
        encoding = value switch
        {
            0x00 => Id3v2TextEncodings.Latin1.Instance,
            0x01 => Id3v2TextEncodings.Utf16WithByteOrderMark.Instance,
            0x02 => Id3v2TextEncodings.Utf16BigEndian.Instance,
            0x03 => Id3v2TextEncodings.Utf8.Instance,
            _ => null,
        };
        return encoding is not null;
    }
}

/// <summary>The four encodings, each a singleton.</summary>
internal static class Id3v2TextEncodings
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16LittleEndian = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16BigEndian = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    /// <summary><c>$00</c>: ISO-8859-1, terminated by <c>$00</c>. Every byte is a character, so it always decodes.</summary>
    public sealed class Latin1 : IId3v2TextEncoding
    {
        public static Latin1 Instance { get; } = new();

        private Latin1()
        {
        }

        public int TerminatorLength => 1;

        public string Decode(ReadOnlySpan<byte> bytes, ref FrameTextState state) => Encoding.Latin1.GetString(bytes);
    }

    /// <summary>
    /// <c>$01</c>: UTF-16 beginning with a byte order mark, terminated by <c>$00 00</c>. Text without a
    /// mark takes the byte order of an earlier string in the frame, or is read as little-endian when
    /// none came before (docs/implementation.md, docs/design/quirks.md).
    /// </summary>
    public sealed class Utf16WithByteOrderMark : IId3v2TextEncoding
    {
        public static Utf16WithByteOrderMark Instance { get; } = new();

        private Utf16WithByteOrderMark()
        {
        }

        public int TerminatorLength => 2;

        public string? Decode(ReadOnlySpan<byte> bytes, ref FrameTextState state)
        {
            if (bytes.Length % 2 != 0)
            {
                return null;
            }

            if (bytes.Length >= 2 && (bytes[0], bytes[1]) is (0xFF, 0xFE) or (0xFE, 0xFF))
            {
                state.BigEndian = bytes[0] == 0xFE;
                bytes = bytes[2..];
            }
            else if (!bytes.IsEmpty)
            {
                state.Quirks |= state.BigEndian is null ? TextQuirks.Utf16WithoutByteOrderMark : TextQuirks.Utf16ByteOrderMarkInherited;
                state.BigEndian ??= false;
            }

            return Strict(state.BigEndian == true ? StrictUtf16BigEndian : StrictUtf16LittleEndian, bytes);
        }
    }

    /// <summary><c>$02</c>: UTF-16 big-endian, without a byte order mark, terminated by <c>$00 00</c>.</summary>
    public sealed class Utf16BigEndian : IId3v2TextEncoding
    {
        public static Utf16BigEndian Instance { get; } = new();

        private Utf16BigEndian()
        {
        }

        public int TerminatorLength => 2;

        public string? Decode(ReadOnlySpan<byte> bytes, ref FrameTextState state) =>
            bytes.Length % 2 != 0 ? null : Strict(StrictUtf16BigEndian, bytes);
    }

    /// <summary><c>$03</c>: UTF-8, terminated by <c>$00</c>.</summary>
    public sealed class Utf8 : IId3v2TextEncoding
    {
        public static Utf8 Instance { get; } = new();

        private Utf8()
        {
        }

        public int TerminatorLength => 1;

        public string? Decode(ReadOnlySpan<byte> bytes, ref FrameTextState state) => Strict(StrictUtf8, bytes);
    }

    private static string? Strict(Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        try
        {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
