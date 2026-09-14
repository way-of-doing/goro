namespace Goro.Hashing;

/// <summary>
/// Computes hashes over the "audio-only" byte range of a file, as determined by
/// TagLibSharp's <see cref="TagLib.File.InvariantStartPosition"/> and
/// <see cref="TagLib.File.InvariantEndPosition"/> — the range that excludes tag
/// sections (e.g. ID3v1, ID3v2) at the start and end of the file.
/// </summary>
public sealed class TagLibAudioHasher : IAudioHasher
{
    public async Task<string> ComputeHashAsync(string filePath, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
    {
        long start;
        long end;
        using (var tagFile = TagLib.File.Create(filePath))
        {
            start = tagFile.InvariantStartPosition;
            end = tagFile.InvariantEndPosition;
        }

        using var hashAlgorithm = algorithm.CreateHashAlgorithm();
        await using var stream = File.OpenRead(filePath);
        stream.Seek(start, SeekOrigin.Begin);

        var remaining = end - start;
        var buffer = new byte[81920];
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            hashAlgorithm.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        hashAlgorithm.TransformFinalBlock([], 0, 0);

        return Convert.ToHexString(hashAlgorithm.Hash!).ToLowerInvariant();
    }
}
