using System.Text.Json;
using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Mp3;
using Goro.Reading.Tags;

namespace Goro.Tests.TestSupport;

/// <summary>
/// The fixture corpus in <c>Fixtures/Audio</c> (see its README) and its manifest, which records what
/// each healthy file holds. The manifest was written by the corpus builder rather than read back by
/// the reader under test, so a misreading cannot land in both the code and its expectation.
/// </summary>
internal static class AudioCorpus
{
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Audio");

    public static string PathOf(string relative) => Path.Combine(Root, relative);

    public static byte[] Bytes(string relative) => File.ReadAllBytes(PathOf(relative));

    /// <summary>Every MP3 in the corpus, seeds included, by its path relative to the corpus.</summary>
    public static IEnumerable<string> Mp3Files =>
        Directory.EnumerateFiles(Root, "*.mp3", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

    private static readonly Lazy<IReadOnlyList<ManifestEntry>> LazyManifest = new(() =>
        JsonSerializer.Deserialize<List<ManifestEntry>>(File.ReadAllText(PathOf("manifest.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);

    public static IReadOnlyList<ManifestEntry> Manifest => LazyManifest.Value;

    public static Analysed Analyse(string relative, ReadPolicy? policy = null) => Analyse(Bytes(relative), policy);

    public static Analysed Analyse(byte[] bytes, ReadPolicy? policy = null)
    {
        var reader = new BoundedReader(new MemoryByteSource(bytes), policy ?? ReadPolicy.Default);
        return new Analysed(Mp3Analysis.Analyse(reader), new TagValues(reader));
    }
}

internal sealed record Analysed(FileLayout Layout, TagValues Values)
{
    public TagIndex Source(TagFormat format) =>
        Layout.Source(format) ?? throw new AssertionException($"The file has no {format} tag.");

    /// <summary>The fields of the source tag of <paramref name="format"/> read under <paramref name="key"/>.</summary>
    public IReadOnlyList<FieldEntry> Fields(TagFormat format, string key) =>
        [.. Source(format).Fields.Where(field => field.Key == key)];

    public FieldEntry Field(TagFormat format, string key) =>
        Fields(format, key) is [var only] ? only : throw new AssertionException($"Expected one {key} in the {format} tag, found {Fields(format, key).Count}.");

    public IReadOnlyList<string>? Text(FieldEntry field) => Values.Text(field) is FieldText.Readable(var values, _) ? values : null;
}

internal sealed record ManifestEntry(string File, string Group, string Description, List<RecordedField>? Recorded);

internal sealed record RecordedField(string Tag, string Key, string? Description, List<string>? Values, string? BytesHex, string? Note);
