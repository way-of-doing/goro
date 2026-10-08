using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
using Goro.Reading.Bytes;

namespace Goro.Reading.Tags;

/// <summary>
/// Reads the values of indexed fields, when something asks for them: a field's content with its
/// storage transformations undone, its text, its description. Every read goes through the file's
/// <see cref="BoundedReader"/> as a payload, so no value larger than the payload limit is read.
/// </summary>
public sealed class TagValues(BoundedReader reader)
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    /// <summary>The field's content, as <c>bytes()</c> gives it.</summary>
    public FieldContent Bytes(FieldEntry field)
    {
        if (field.RunsPastTag)
        {
            return new FieldContent.Unreadable(UnreadableReason.RunsPastTag);
        }

        if (!TryReadStored(field.Stored, out var stored))
        {
            return new FieldContent.Unreadable(UnreadableReason.TooLarge);
        }

        return Undo(stored, field.Transform);
    }

    /// <summary>The field's text, as <c>field()</c> gives it.</summary>
    public FieldText Text(FieldEntry field)
    {
        if (field.Form is FieldForm.Id3v2Binary or FieldForm.ApeBinary or FieldForm.Id3v1Byte)
        {
            return new FieldText.NotText();
        }

        var read = Bytes(field);
        if (read is FieldContent.Unreadable(var cannot))
        {
            return new FieldText.Unreadable(cannot);
        }

        var bytes = ((FieldContent.Readable)read).Bytes.Span;
        switch (field.Form)
        {
            case FieldForm.Id3v2Url:
                return new FieldText.Readable([Encoding.Latin1.GetString(bytes)]);
            case FieldForm.ApeUtf8Text:
                return Utf8Values(bytes);
            case FieldForm.ApeLatin1Text:
                return new FieldText.Readable([.. Encoding.Latin1.GetString(bytes).Split('\0')]);
            case FieldForm.Id3v1Text:
                return new FieldText.Readable([Id3v1Text(bytes)]);
        }

        // An Id3v2 frame holding text: an encoding byte, perhaps a language and a description, then the values.
        if (!TryEncoding(bytes, out var encoding, out var unknown))
        {
            return new FieldText.Unreadable(unknown);
        }

        var state = new FrameTextState();
        var rest = bytes[1..];
        if (field.Form is not FieldForm.Id3v2Text)
        {
            if (ReadDescription(field.Form, encoding, ref state, ref rest, out _) is { } reason)
            {
                return new FieldText.Unreadable(reason);
            }

            if (field.Form is FieldForm.Id3v2DescribedUrl)
            {
                return new FieldText.Readable([Encoding.Latin1.GetString(rest)], state.Quirks);
            }
        }

        return encoding.DecodeValues(rest, ref state) is { } values
            ? new FieldText.Readable(values, state.Quirks)
            : new FieldText.Unreadable(UnreadableReason.DoesNotDecode);
    }

    /// <summary>The description of a <c>TXXX</c>, <c>WXXX</c>, <c>COMM</c> or <c>USLT</c> frame.</summary>
    public FieldDescription Description(FieldEntry field)
    {
        if (field.Form is not (FieldForm.Id3v2DescribedText or FieldForm.Id3v2LanguageText or FieldForm.Id3v2DescribedUrl))
        {
            return new FieldDescription.None();
        }

        var read = Bytes(field);
        if (read is FieldContent.Unreadable(var cannot))
        {
            return new FieldDescription.Unreadable(cannot);
        }

        var bytes = ((FieldContent.Readable)read).Bytes.Span;
        if (!TryEncoding(bytes, out var encoding, out var unknown))
        {
            return new FieldDescription.Unreadable(unknown);
        }

        var state = new FrameTextState();
        var rest = bytes[1..];
        return ReadDescription(field.Form, encoding, ref state, ref rest, out var description) is { } reason
            ? new FieldDescription.Unreadable(reason)
            : new FieldDescription.Readable(description, state.Quirks);
    }

    private bool TryReadStored(StoredBytes stored, out ReadOnlyMemory<byte> bytes)
    {
        switch (stored)
        {
            case StoredBytes.InMemory(var memory):
                bytes = memory;
                return true;
            case StoredBytes.InFile(var region):
                return reader.TryRead(ReadPurpose.Payload, region.Start, region.Length, out bytes);
            default:
                throw new InvalidOperationException("Stored bytes are either in the file or in memory.");
        }
    }

    /// <summary>Undoes what an Id3v2 frame's flags declare, in the order the specification lays them out.</summary>
    private FieldContent Undo(ReadOnlyMemory<byte> stored, FieldTransform transform)
    {
        if (transform.PrefixLength > stored.Length)
        {
            return new FieldContent.Unreadable(UnreadableReason.ShorterThanItsFlags);
        }

        var data = stored[transform.PrefixLength..];
        if (transform.Unsynchronised)
        {
            data = Binary.Resynchronise(data.Span);
        }

        if (transform.Encrypted)
        {
            return new FieldContent.Unreadable(UnreadableReason.Encrypted);
        }

        return transform.Compressed ? Inflate(data) : new FieldContent.Readable(data);
    }

    /// <summary>Decompresses zlib data, refusing to produce more than the payload limit allows.</summary>
    private FieldContent Inflate(ReadOnlyMemory<byte> compressed)
    {
        var limit = reader.Policy.PayloadLimit;
        try
        {
            using var input = new MemoryStream(compressed.ToArray(), writable: false);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = zlib.Read(buffer)) > 0)
            {
                if (output.Length + read > limit)
                {
                    return new FieldContent.Unreadable(UnreadableReason.TooLarge);
                }

                output.Write(buffer, 0, read);
            }

            return new FieldContent.Readable(output.ToArray());
        }
        catch (InvalidDataException)
        {
            return new FieldContent.Unreadable(UnreadableReason.DoesNotDecompress);
        }
    }

    /// <summary>The encoding a frame's first byte names, or why there is none to read its text in.</summary>
    private static bool TryEncoding(ReadOnlySpan<byte> content, [NotNullWhen(true)] out IId3v2TextEncoding? encoding, out UnreadableReason reason)
    {
        encoding = null;
        reason = content.IsEmpty ? UnreadableReason.NoEncoding : UnreadableReason.UnknownEncoding;
        return !content.IsEmpty && IId3v2TextEncoding.FromByte(content[0], out encoding);
    }

    /// <summary>
    /// Reads a frame's language, where it has one, and its description, leaving <paramref name="rest"/>
    /// at what follows. The description must itself be readable: a frame whose description cannot be
    /// read cannot be told apart from others by it, so it is unusable for every description asked about.
    /// </summary>
    private static UnreadableReason? ReadDescription(
        FieldForm form, IId3v2TextEncoding encoding, ref FrameTextState state, ref ReadOnlySpan<byte> rest, out string description)
    {
        description = "";
        if (form is FieldForm.Id3v2LanguageText)
        {
            if (rest.Length < 3)
            {
                return UnreadableReason.NoLanguage;
            }

            rest = rest[3..];
        }

        var terminator = encoding.FindTerminator(rest);
        if (terminator < 0)
        {
            return UnreadableReason.DescriptionUnterminated;
        }

        if (encoding.Decode(rest[..terminator], ref state) is not { } decoded)
        {
            return UnreadableReason.DoesNotDecode;
        }

        description = decoded;
        rest = rest[(terminator + encoding.TerminatorLength)..];
        return null;
    }

    private static FieldText Utf8Values(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return new FieldText.Readable([.. StrictUtf8.GetString(bytes).Split('\0')]);
        }
        catch (DecoderFallbackException)
        {
            return new FieldText.Unreadable(UnreadableReason.DoesNotDecode);
        }
    }

    /// <summary>
    /// An Id3v1 field's text: ISO-8859-1, up to the first NUL, without the spaces that pad it out.
    /// What follows a NUL is padding, whatever it holds.
    /// </summary>
    private static string Id3v1Text(ReadOnlySpan<byte> bytes)
    {
        var nul = bytes.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul < 0 ? bytes : bytes[..nul]).TrimEnd(' ');
    }
}
