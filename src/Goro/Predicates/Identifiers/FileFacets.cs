using Goro.Reading;
using Goro.Reading.Tags;

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

    public override FileMetadata Load(FileData file)
    {
        // The path is the link's, but the audio file is its target, and FileInfo of a symbolic link
        // describes the link itself. A link whose target is gone fails here, as it should.
        var info = new FileInfo(file.Path);
        var target = info.LinkTarget is null ? info : (FileInfo)info.ResolveLinkTarget(returnFinalTarget: true)!;
        return new FileMetadata(target.Length);
    }
}

/// <summary>The properties of a file's audio, as opposed to its tags.</summary>
internal sealed record AudioProperties(TimeSpan Duration);

/// <summary>
/// A file's audio properties, read by TagLibSharp until Goro's reader gives the playing time (step 3
/// of docs/directions/mp3-support.md). Only the properties are kept; the file is closed before this
/// returns. The file's layout is analysed first, as for the tags, so that a file with no audio is
/// unreadable here too, and counts as opened.
/// </summary>
internal sealed class AudioPropertiesFacet : FileFacet<AudioProperties>
{
    public static AudioPropertiesFacet Instance { get; } = new();

    private AudioPropertiesFacet()
    {
    }

    public override AudioProperties Load(FileData file)
    {
        TagsFacet.ReadableLayout(file);
        using var taglib = TagLib.File.Create(file.Path, TagLib.ReadStyle.Average);
        return taglib.Properties is { } properties
            ? new AudioProperties(properties.Duration)
            : throw new InvalidDataException("The audio properties could not be read.");
    }
}

/// <summary>A file's tags, as its layout indexes them, and the values that can be read from them.</summary>
internal sealed record FileTags(FileLayout Layout, TagValues Values)
{
    /// <summary>The tag a source of <paramref name="format"/> reads, if the file has one it can read.</summary>
    public TagIndex? Source(TagFormat format) =>
        Layout.Source(format) is { State: not TagState.Unusable } tag ? tag : null;
}

/// <summary>
/// A file's tags, from the analysis of its edges. A file in which no audio can be found is a file
/// that cannot be read (docs/concepts/warnings.md), whichever identifier asked.
/// </summary>
internal sealed class TagsFacet : FileFacet<FileTags>
{
    public static TagsFacet Instance { get; } = new();

    private TagsFacet()
    {
    }

    public override FileTags Load(FileData file) => new(ReadableLayout(file), file.Loader.Values);

    /// <summary>The file's layout, or the file is unreadable because no audio can be found in it.</summary>
    /// <exception cref="UnreadableFileException">No audio was found.</exception>
    public static FileLayout ReadableLayout(FileData file) =>
        file.Loader.Layout is { Audio: AudioLocation.NotFound } ? throw new UnreadableFileException(file.Path, "no MPEG audio found") : file.Loader.Layout;
}
