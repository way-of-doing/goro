using Goro.Reading.Bytes;

namespace Goro.Reading.Tags;

/// <summary>
/// Locates the fixed fields of an Id3v1 tag: 128 bytes at the end of the file, starting <c>TAG</c>.
/// Id3v1.1 takes the last two bytes of the comment for a NUL and a track number, which is how the
/// revisions are told apart; a v1.0 tag has no track field at all.
/// </summary>
public static class Id3v1Index
{
    public const int Length = 128;

    public static TagRead? TryRead(BoundedReader reader, long at)
    {
        if (!reader.TryRead(ReadPurpose.Sniff, at, Length, out var bytes) || !bytes.Span.StartsWith("TAG"u8))
        {
            return null;
        }

        var tag = bytes.Span;
        var v11 = tag[125] == 0 && tag[126] != 0;
        var kind = new TagKind(TagFormat.Id3v1, v11 ? 1 : 0);
        FieldEntry Field(string key, FieldForm form, int offset, int length) =>
            new(key, form, new StoredBytes.InFile(new Region(at + offset, length)));

        List<FieldEntry> fields =
        [
            Field("title", FieldForm.Id3v1Text, 3, 30),
            Field("artist", FieldForm.Id3v1Text, 33, 30),
            Field("album", FieldForm.Id3v1Text, 63, 30),
            Field("year", FieldForm.Id3v1Text, 93, 4),
            Field("comment", FieldForm.Id3v1Text, 97, v11 ? 28 : 30),
        ];
        if (v11)
        {
            fields.Add(Field("track", FieldForm.Id3v1Byte, 126, 1));
        }

        fields.Add(Field("genre", FieldForm.Id3v1Byte, 127, 1));
        return new TagRead(new TagIndex(kind, new Region(at, Length), TagState.Intact, [.. fields]), [], SizeTrusted: true);
    }
}
