namespace Goro.Reading.Tags;

/// <summary>
/// What kind of datum a field is, which decides how its text is read, if it has any. Every form
/// but the Id3v1 ones is read from content whose storage transformations have been undone.
/// </summary>
public enum FieldForm
{
    /// <summary>An Id3v2 text frame: an encoding byte, then values separated by terminators.</summary>
    Id3v2Text,

    /// <summary><c>TXXX</c>: an encoding byte, a description, then values.</summary>
    Id3v2DescribedText,

    /// <summary><c>COMM</c> and <c>USLT</c>: an encoding byte, a language, a description, then values.</summary>
    Id3v2LanguageText,

    /// <summary>A URL frame: the URL in Latin-1, with no encoding byte.</summary>
    Id3v2Url,

    /// <summary><c>WXXX</c>: an encoding byte, a description, then the URL in Latin-1.</summary>
    Id3v2DescribedUrl,

    /// <summary>Any other Id3v2 frame, which holds no text.</summary>
    Id3v2Binary,

    /// <summary>An APEv2 text or locator item: UTF-8, values separated by NUL.</summary>
    ApeUtf8Text,

    /// <summary>An APEv1 item, read as ISO-8859-1 (docs/implementation.md).</summary>
    ApeLatin1Text,

    /// <summary>An APE item flagged as binary, or with the reserved kind.</summary>
    ApeBinary,

    /// <summary>A fixed-width Id3v1 text field, padded with NUL bytes or spaces.</summary>
    Id3v1Text,

    /// <summary>A single-byte Id3v1 field: the track and the genre.</summary>
    Id3v1Byte,
}

/// <summary>Where a field's stored bytes are.</summary>
public abstract record StoredBytes
{
    private StoredBytes()
    {
    }

    /// <summary>In the file, to be read when asked for.</summary>
    public sealed record InFile(Region Region) : StoredBytes
    {
        public override long Length => Region.Length;
    }

    /// <summary>
    /// Already in memory: the field belongs to an Id3v2 tag that had to be resynchronised as a whole
    /// before its frames could be found, so its stored bytes exist nowhere in the file as they are.
    /// </summary>
    public sealed record InMemory(ReadOnlyMemory<byte> Bytes) : StoredBytes
    {
        public override long Length => Bytes.Length;
    }

    public abstract long Length { get; }
}

/// <summary>
/// The storage transformations an Id3v2 frame's flags declare, which <c>bytes()</c> undoes:
/// <paramref name="PrefixLength"/> bytes in front of the content (a group identifier, an encryption
/// method, a data length or decompressed size), then unsynchronisation, encryption and compression.
/// </summary>
public readonly record struct FieldTransform(int PrefixLength, bool Unsynchronised, bool Encrypted, bool Compressed)
{
    public static FieldTransform None => default;
}

/// <summary>
/// One field of a tag, located but not read: an Id3v2 frame, an APE item, an Id3v1 field.
/// </summary>
/// <param name="Key">
/// The name Goro reads the field under: an Id3v2 frame's v2.4 identifier where it has one (see
/// <see cref="Id3v2FrameNames"/>), an APE key as recorded, an Id3v1 field's name.
/// </param>
/// <param name="RunsPastTag">
/// The field's recorded size runs past the end of its tag. It is clearly recorded and cannot be
/// read, so it is an unusable occurrence, and the tag's structure breaks there.
/// </param>
public sealed record FieldEntry(string Key, FieldForm Form, StoredBytes Stored, FieldTransform Transform = default, bool RunsPastTag = false);
