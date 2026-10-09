using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Mp3;

namespace Goro.Hashing;

/// <summary>
/// Hashes an MP3's encoded audio stream and nothing the container adds around it: the frames from the
/// first audio frame to the end of the last whole frame, as <see cref="Mp3AudioRange"/> finds them
/// (docs/commands/hash.md). The file's edges are analysed first, as for any other command, so a file
/// with no audio has no hash, and damage to its tags is reported.
/// </summary>
public sealed class Mp3AudioHasher(ReadPolicy policy) : IAudioHasher
{
    private const int BufferLength = 80 * 1024;

    public Task<AudioHash> ComputeHashAsync(string filePath, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
    {
        using var source = new FileByteSource(filePath);
        var layout = Mp3Analysis.Analyse(new BoundedReader(source, policy));
        if (layout.Audio is not AudioLocation.Found)
        {
            throw new InvalidDataException("no MPEG audio found");
        }

        var range = Mp3AudioRange.Find(source, layout);
        using var hash = algorithm.CreateHashAlgorithm();
        var buffer = new byte[BufferLength];
        for (var at = range.Start; at < range.End; )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(BufferLength, range.End - at);
            if (source.Read(at, buffer.AsSpan(0, count)) != count)
            {
                throw new IOException("The file changed while it was being read.");
            }

            hash.TransformBlock(buffer, 0, count, null, 0);
            at += count;
        }

        hash.TransformFinalBlock([], 0, 0);
        return Task.FromResult(new AudioHash(Convert.ToHexStringLower(hash.Hash!), layout.IsIncomplete));
    }
}
