namespace Goro.Hashing;

/// <summary>
/// A file's audio hash, and whether part of the file could not be read on the way to it: what the
/// <c>incomplete</c> warning counts (docs/concepts/warnings.md).
/// </summary>
public sealed record AudioHash(string Hex, bool Incomplete);

public interface IAudioHasher
{
    /// <summary>Hashes the audio of the file at <paramref name="filePath"/>, as docs/commands/hash.md describes.</summary>
    /// <exception cref="Exception">The file cannot be read, or holds no audio: anything thrown means it has no hash.</exception>
    Task<AudioHash> ComputeHashAsync(string filePath, HashAlgorithmKind algorithm, CancellationToken cancellationToken);
}
