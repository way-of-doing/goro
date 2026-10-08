using System.Collections.Immutable;
using Goro.Predicates.Values;
using Goro.Reading.Tags;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The bindings of what reads tags: the source functions, which read a tag's own data as recorded,
/// and the cells of the concepts. See docs/features/builtins/identifiers.md.
/// </summary>
/// <remarks>
/// Every binding reads the file's tags through <see cref="TagsFacet"/>, so a file in which no audio
/// can be found is unreadable whichever binding asks. A source reads only its source tag: the first
/// of its format in the file, and nothing from a tag that cannot be read at all. The concepts are
/// bound to <see cref="NotImplemented{T}"/> until they interpret what they read (step 2c of
/// docs/directions/mp3-support.md).
/// </remarks>
internal static class TagBindings
{
    public static IdentifierBinding<T> NotImplemented<T>(DeclaredName name) where T : notnull =>
        new NotImplementedBinding<T>(name);

    /// <summary>The binding of <c>field()</c> for a source.</summary>
    public static Func<SourceCallName, IdentifierBinding<string>> Field(string source) => source switch
    {
        "id3v2" => call => new Id3v2Field(call.Arguments[0], call.Arguments.Length > 1 ? call.Arguments[1] : null),
        "ape" => call => new ApeField(call.Arguments[0]),
        "vorbis" => _ => new NoVorbisComments<string>(),
        _ => throw new ArgumentException($"The {source} source has no field()."),
    };

    /// <summary>The binding of <c>bytes()</c> for a source.</summary>
    public static Func<SourceCallName, IdentifierBinding<Blob>> Bytes(string source) => source switch
    {
        "id3v2" => call => new Id3v2Bytes(call.Arguments[0]),
        "ape" => call => new ApeBytes(call.Arguments[0]),
        "vorbis" => _ => new NoVorbisComments<Blob>(),
        _ => throw new ArgumentException($"The {source} source has no bytes()."),
    };

    private static Value<string> Text(FileTags tags, FieldEntry field, Origin origin) =>
        tags.Values.Text(field) is FieldText.Readable(var values, _)
            ? Value<string>.Of(values.Select(Occurrence<string> (text) => new Usable<string>(text)))
            : Value<string>.Single(new Unusable<string>(origin));

    private static Occurrence<Blob> Content(FileTags tags, FieldEntry field, Origin origin) =>
        tags.Values.Bytes(field) is FieldContent.Readable(var bytes)
            ? new Usable<Blob>(new Blob([.. bytes.Span]))
            : new Unusable<Blob>(origin);

    /// <summary>
    /// The text of every frame named <paramref name="frame"/>, and of only those whose description is
    /// <paramref name="description"/> where one is given. A frame whose description cannot be read
    /// might have any description, so it is unusable whatever description was asked about.
    /// </summary>
    private sealed class Id3v2Field(string frame, string? description) : IdentifierBinding<string>
    {
        public override Value<string> Resolve(FileData file, Origin origin)
        {
            var tags = file.Get(TagsFacet.Instance);
            if (tags.Source(TagFormat.Id3v2) is not { } tag)
            {
                return Value<string>.Absent;
            }

            var occurrences = ImmutableArray.CreateBuilder<Occurrence<string>>();
            foreach (var field in tag.Fields.Where(f => f.Key == frame))
            {
                if (description is not null)
                {
                    switch (tags.Values.Description(field))
                    {
                        case FieldDescription.Readable(var text, _) when !string.Equals(text, description, StringComparison.OrdinalIgnoreCase):
                            continue;
                        case FieldDescription.Unreadable:
                            occurrences.Add(new Unusable<string>(origin));
                            continue;
                    }
                }

                occurrences.AddRange(Text(tags, field, origin).Occurrences);
            }

            return Value<string>.Of(occurrences);
        }
    }

    /// <summary>The content of every frame named <paramref name="frame"/>, one occurrence each.</summary>
    private sealed class Id3v2Bytes(string frame) : IdentifierBinding<Blob>
    {
        public override Value<Blob> Resolve(FileData file, Origin origin)
        {
            var tags = file.Get(TagsFacet.Instance);
            return tags.Source(TagFormat.Id3v2) is { } tag
                ? Value<Blob>.Of(tag.Fields.Where(f => f.Key == frame).Select(field => Content(tags, field, origin)))
                : Value<Blob>.Absent;
        }
    }

    /// <summary>
    /// The item an APE key names, matched without regard to case. APE forbids two keys differing only
    /// in case, but files holding them exist, and only the last of them is seen.
    /// </summary>
    private static FieldEntry? ApeItem(FileTags tags, string key) =>
        tags.Source(TagFormat.Ape)?.Fields.LastOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The values of an APE item: an item flagged as binary has no text, and is unusable.</summary>
    private sealed class ApeField(string key) : IdentifierBinding<string>
    {
        public override Value<string> Resolve(FileData file, Origin origin)
        {
            var tags = file.Get(TagsFacet.Instance);
            return ApeItem(tags, key) is { } item ? Text(tags, item, origin) : Value<string>.Absent;
        }
    }

    /// <summary>The value of an APE item as recorded, whatever its kind, as a single occurrence.</summary>
    private sealed class ApeBytes(string key) : IdentifierBinding<Blob>
    {
        public override Value<Blob> Resolve(FileData file, Origin origin)
        {
            var tags = file.Get(TagsFacet.Instance);
            return ApeItem(tags, key) is { } item ? Value<Blob>.Single(Content(tags, item, origin)) : Value<Blob>.Absent;
        }
    }

    /// <summary>
    /// The Vorbis comments of an MP3, which has none. The file's tags are still read, so that a file
    /// in which no audio can be found is unreadable here as everywhere else.
    /// </summary>
    private sealed class NoVorbisComments<T> : IdentifierBinding<T> where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin)
        {
            file.Get(TagsFacet.Instance);
            return Value<T>.Absent;
        }
    }

    private sealed class NotImplementedBinding<T>(DeclaredName name) : IdentifierBinding<T> where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin) =>
            throw new NotSupportedException($"Reading tags is not implemented yet, so {name} cannot be read.");
    }
}
