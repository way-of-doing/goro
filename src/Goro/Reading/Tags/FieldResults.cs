using System.Collections.Immutable;

namespace Goro.Reading.Tags;

/// <summary>Why a field that is recorded could not be read: what makes it an unusable occurrence.</summary>
public enum UnreadableReason
{
    /// <summary>Its recorded size runs past the end of its tag.</summary>
    RunsPastTag,

    /// <summary>Its content, or what it decompresses to, is over the payload limit.</summary>
    TooLarge,

    /// <summary>It is encrypted, which no reader can undo.</summary>
    Encrypted,

    /// <summary>It is compressed, and the compressed data is not valid.</summary>
    DoesNotDecompress,

    /// <summary>It is shorter than the bytes its flags say come in front of its content.</summary>
    ShorterThanItsFlags,

    /// <summary>A text frame with no content at all, not even its encoding byte.</summary>
    NoEncoding,

    /// <summary>An encoding byte the format does not define.</summary>
    UnknownEncoding,

    /// <summary>Text that is not valid in the encoding declared for it.</summary>
    DoesNotDecode,

    /// <summary>A frame too short to hold the language it should.</summary>
    NoLanguage,

    /// <summary>A description with no terminator after it.</summary>
    DescriptionUnterminated,
}

/// <summary>
/// What had to be assumed to read a field's text, for <c>goro audit</c>
/// (docs/implementation.md, docs/design/quirks.md).
/// </summary>
[Flags]
public enum TextQuirks
{
    None = 0,

    /// <summary>UTF-16 text without a byte order mark, read as little-endian.</summary>
    Utf16WithoutByteOrderMark = 1,

    /// <summary>A later UTF-16 string without a mark of its own, read in the frame's first byte order.</summary>
    Utf16ByteOrderMarkInherited = 2,
}

/// <summary>A field's content, as <c>bytes()</c> reads it.</summary>
public abstract record FieldContent
{
    private FieldContent()
    {
    }

    public sealed record Readable(ReadOnlyMemory<byte> Bytes) : FieldContent;

    public sealed record Unreadable(UnreadableReason Reason) : FieldContent;
}

/// <summary>A field's text, as <c>field()</c> reads it: one string for each value the format records.</summary>
public abstract record FieldText
{
    private FieldText()
    {
    }

    public sealed record Readable(ImmutableArray<string> Values, TextQuirks Quirks = TextQuirks.None) : FieldText;

    public sealed record Unreadable(UnreadableReason Reason) : FieldText;

    /// <summary>The field holds no text: an Id3v2 frame that is not a text frame, an APE binary item.</summary>
    public sealed record NotText : FieldText;
}

/// <summary>The description of a <c>TXXX</c>, <c>WXXX</c>, <c>COMM</c> or <c>USLT</c> frame.</summary>
public abstract record FieldDescription
{
    private FieldDescription()
    {
    }

    public sealed record Readable(string Text, TextQuirks Quirks = TextQuirks.None) : FieldDescription;

    public sealed record Unreadable(UnreadableReason Reason) : FieldDescription;

    /// <summary>A field of a form that carries no description.</summary>
    public sealed record None : FieldDescription;
}
