namespace Goro.Tests.TestSupport;

/// <summary>
/// A temporary directory of audio files for CLI tests, including the kinds of file Goro cannot
/// read. Paths it returns are full paths, as discovery yields them.
/// </summary>
internal sealed class TempCollection : IDisposable
{
    private readonly List<string> _locked = [];

    public TempCollection(string prefix) => Root = Directory.CreateTempSubdirectory(prefix).FullName;

    public string Root { get; }

    public string PathOf(params string[] relativePath) => Path.GetFullPath(Path.Combine([Root, .. relativePath]));

    /// <summary>A small, valid MP3, which Goro can read and hash.</summary>
    public string Mp3(params string[] relativePath) =>
        Write(SyntheticMp3Builder.BuildMp3(SyntheticMp3Builder.BuildAudioFrames(), SyntheticMp3Builder.BuildId3V2(20)), relativePath);

    /// <summary>A file named as an MP3 that holds text, and so no audio.</summary>
    public string NotAudio(params string[] relativePath) =>
        Write("not audio at all\n"u8.ToArray(), relativePath);

    /// <summary>
    /// An MP3 cut short inside its Id3v2 tag, so that no audio follows. A
    /// file cut short part way through its last audio frame is read without complaint, and is not
    /// what this is.
    /// </summary>
    public string CutShortInsideItsTag(params string[] relativePath)
    {
        var whole = SyntheticMp3Builder.BuildMp3(SyntheticMp3Builder.BuildAudioFrames(), SyntheticMp3Builder.BuildId3V2(5000));
        return Write(whole[..3000], relativePath);
    }

    public string Subdirectory(params string[] relativePath) => Directory.CreateDirectory(PathOf(relativePath)).FullName;

    /// <summary>
    /// Takes every permission away from a file or directory until <see cref="Unlock"/> or disposal.
    /// Marks the test inconclusive where permissions are not enforced, as on Windows or for root.
    /// </summary>
    public string Lock(string path)
    {
        Assume.That(OperatingSystem.IsWindows(), Is.False, "Unix permissions are needed");
        File.SetUnixFileMode(path, UnixFileMode.None);
        _locked.Add(path);
        Assume.That(() =>
        {
            if (Directory.Exists(path))
            {
                Directory.EnumerateFileSystemEntries(path).Any();
            }
            else
            {
                File.OpenRead(path).Dispose();
            }
        }, Throws.Exception, "permissions are not enforced for this user");
        return path;
    }

    public void Unlock(string path)
    {
        File.SetUnixFileMode(path, Directory.Exists(path)
            ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        _locked.Remove(path);
    }

    public void Dispose()
    {
        foreach (var path in _locked.ToList())
        {
            Unlock(path);
        }

        Directory.Delete(Root, recursive: true);
    }

    private string Write(byte[] content, string[] relativePath)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }
}
