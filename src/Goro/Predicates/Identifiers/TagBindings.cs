using System.Collections.Immutable;
using Goro.Predicates.Identifiers.Interpretation;
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
/// of its format in the file, and nothing from a tag that cannot be read at all. A cell reads what
/// the source function for its field reads, and interprets it.
/// </remarks>
internal static class TagBindings
{
    /// <summary>The binding of a concept's cell: <paramref name="field"/> of <paramref name="source"/>, interpreted.</summary>
    public static IdentifierBinding<T> Cell<T>(string source, string field, ConceptInterpretation<T> interpretation) where T : notnull =>
        source switch
        {
            SourceNames.Id3v2 => new Id3v2Cell<T>(field, interpretation.Id3v2 ?? interpretation.Text),
            SourceNames.Ape => new ApeCell<T>(field, interpretation.Text),
            SourceNames.Id3v1 => new Id3v1Cell<T>(field, interpretation),
            SourceNames.Vorbis => new NoVorbisComments<T>(),
            _ => throw new ArgumentException($"The {source} source has no concepts."),
        };

    /// <summary>The binding of <c>field()</c> for a source.</summary>
    public static Func<SourceCallName, IdentifierBinding<string>> Field(string source) => source switch
    {
        SourceNames.Id3v2 => call => new Id3v2Field(call.Arguments[0], call.Arguments.Length > 1 ? call.Arguments[1] : null),
        SourceNames.Ape => call => new ApeField(call.Arguments[0]),
        SourceNames.Vorbis => _ => new NoVorbisComments<string>(),
        _ => throw new ArgumentException($"The {source} source has no field()."),
    };

    /// <summary>The binding of <c>bytes()</c> for a source.</summary>
    public static Func<SourceCallName, IdentifierBinding<Blob>> Bytes(string source) => source switch
    {
        SourceNames.Id3v2 => call => new Id3v2Bytes(call.Arguments[0]),
        SourceNames.Ape => call => new ApeBytes(call.Arguments[0]),
        SourceNames.Vorbis => _ => new NoVorbisComments<Blob>(),
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

    /// <summary>
    /// Every value of the frames a cell reads, interpreted. A frame that cannot be read is one
    /// unusable occurrence, as it is to <c>field()</c>.
    /// </summary>
    private static IEnumerable<Occurrence<T>> Interpret<T>(
        FileTags tags, FieldEntry field, Origin origin, Func<string, Origin, IEnumerable<Occurrence<T>>> interpret) where T : notnull =>
        tags.Values.Text(field) is FieldText.Readable(var values, _)
            ? values.SelectMany(value => interpret(value, origin))
            : [new Unusable<T>(origin)];

    /// <summary>An Id3v2 concept: every frame named <paramref name="frame"/>, interpreted.</summary>
    private sealed class Id3v2Cell<T>(string frame, Func<string, Origin, IEnumerable<Occurrence<T>>> interpret) : IdentifierBinding<T>
        where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin)
        {
            var tags = file.Get(TagsFacet.Instance);
            return tags.Source(TagFormat.Id3v2) is { } tag
                ? Value<T>.Of(tag.Fields.Where(f => f.Key == frame).SelectMany(field => Interpret(tags, field, origin, interpret)))
                : Value<T>.Absent;
        }
    }

    /// <summary>An APE concept: the item <c>ape::field()</c> would read under its key, interpreted.</summary>
    private sealed class ApeCell<T>(string key, Func<string, Origin, IEnumerable<Occurrence<T>>> interpret) : IdentifierBinding<T>
        where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin)
        {
            var tags = file.Get(TagsFacet.Instance);
            return ApeItem(tags, key) is { } item ? Value<T>.Of(Interpret(tags, item, origin, interpret)) : Value<T>.Absent;
        }
    }

    /// <summary>
    /// An Id3v1 concept. Every field is present in every tag, so one that is padding all the way
    /// through records nothing, and is absent whatever the concept's type (docs/features/builtins/identifiers.md,
    /// "A fixed structure"). A field recorded as a byte is interpreted as one.
    /// </summary>
    private sealed class Id3v1Cell<T>(string field, ConceptInterpretation<T> interpretation) : IdentifierBinding<T>
        where T : notnull
    {
        public override Value<T> Resolve(FileData file, Origin origin)
        {
            var tags = file.Get(TagsFacet.Instance);
            if (tags.Source(TagFormat.Id3v1)?.Fields.FirstOrDefault(f => f.Key == field) is not { } entry)
            {
                // Id3v1.0 has no track field at all.
                return Value<T>.Absent;
            }

            if (entry.Form == FieldForm.Id3v1Byte)
            {
                var read = tags.Values.Bytes(entry) is FieldContent.Readable(var bytes) && bytes.Length == 1 ? bytes.Span[0] : (byte?)null;
                return read is { } value && interpretation.Id3v1Byte?.Invoke(value, origin) is { } occurrence
                    ? Value<T>.Single(occurrence)
                    : Value<T>.Absent;
            }

            // The reader has already left out the padding, so nothing at all means padding all through.
            return tags.Values.Text(entry) is FieldText.Readable([var text], _) && text.Length == 0
                ? Value<T>.Absent
                : Value<T>.Of(Interpret(tags, entry, origin, interpretation.Text));
        }
    }
}
