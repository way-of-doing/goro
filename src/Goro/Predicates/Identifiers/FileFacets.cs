namespace Goro.Predicates.Identifiers;

/// <summary>What the file system records about a file, without opening it.</summary>
internal sealed record FileMetadata(long Length);

/// <summary>
/// A file's metadata: the cheapest thing a predicate can need beyond the path, and enough for
/// <c>file::size</c>. Nothing is opened, so a file that may not be read still has a size.
/// </summary>
internal sealed class FileSystemFacet : FileFacet<FileMetadata>
{
    public static FileSystemFacet Instance { get; } = new();

    private FileSystemFacet()
    {
    }

    public override FileMetadata Load(string path)
    {
        // The path is the link's, but the audio file is its target, and FileInfo of a symbolic link
        // describes the link itself. A link whose target is gone fails here, as it should.
        var info = new FileInfo(path);
        var file = info.LinkTarget is null ? info : (FileInfo)info.ResolveLinkTarget(returnFinalTarget: true)!;
        return new FileMetadata(file.Length);
    }
}

/// <summary>The properties of a file's audio, as opposed to its tags.</summary>
internal sealed record AudioProperties(TimeSpan Duration);

/// <summary>
/// A file's audio properties, read by TagLibSharp. Only the properties are kept; the file is closed
/// before this returns.
/// </summary>
/// <remarks>
/// TagLibSharp reads the tags while it locates the audio, so this facet parses them too, and could
/// in principle find a file unreadable for <c>file::duration</c> on account of its tags. Damaged
/// Id3v2 tags of many shapes were tried and TagLibSharp tolerated all of them, so no such file is
/// known. Whether this facet and the tag facets share one read is for the tag namespaces' line.
/// </remarks>
internal sealed class AudioPropertiesFacet : FileFacet<AudioProperties>
{
    public static AudioPropertiesFacet Instance { get; } = new();

    private AudioPropertiesFacet()
    {
    }

    public override AudioProperties Load(string path)
    {
        using var file = TagLib.File.Create(path, TagLib.ReadStyle.Average);
        return file.Properties is { } properties
            ? new AudioProperties(properties.Duration)
            : throw new InvalidDataException("The audio properties could not be read.");
    }
}
