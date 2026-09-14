namespace Goro.Hashing;

public interface IAudioHasher
{
    Task<string> ComputeHashAsync(string filePath, HashAlgorithmKind algorithm, CancellationToken cancellationToken);
}
