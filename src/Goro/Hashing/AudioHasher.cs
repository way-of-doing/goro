using Goro.Discovery;
using Goro.Reading;
using Goro.Reading.Bytes;
using Goro.Reading.Flac;
using Goro.Reading.Mp3;

namespace Goro.Hashing;

/// <summary>
/// Hashes a file's encoded audio stream and nothing the container adds around it (docs/commands/hash.md):
/// for an MP3, the frames from the first audio frame to the end of the last whole frame, as
/// <see cref="Mp3AudioRange"/> finds them; for a FLAC file, its frames from the first to the end of
/// the last whole one, as <see cref="FlacAudioRange"/> finds them, checking each frame's checksums on
/// the way. The file's edges are analysed first, as for any other command, as the format its
/// extension names, so a file with no audio has no hash, and damage to its tags is reported.
/// </summary>
/// <remarks>
/// The faults the FLAC walk records are not reported here: they are for <c>goro audit</c> and
/// <c>goro explore</c> (docs/design/rationale.md).
/// </remarks>
public sealed class AudioHasher(ReadPolicy policy) : IAudioHasher
{
    private const int BufferLength = 80 * 1024;

    public Task<AudioHash> ComputeHashAsync(string filePath, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
    {
        var format = CandidateFiles.FormatOf(filePath) ?? throw new InvalidDataException("not a format Goro reads");
        using var source = new FileByteSource(filePath);
        var layout = AudioFormats.Analyse(format, new BoundedReader(source, policy));
        if (layout.Audio is not AudioLocation.Found)
        {
            throw new InvalidDataException(AudioFormats.NoAudioFound(format));
        }

        var range = format switch
        {
            AudioFormat.Mp3 => Mp3AudioRange.Find(source, layout),
            AudioFormat.Flac => FlacAudioRange.Find(source, layout).Range,
            _ => throw new ArgumentOutOfRangeException(nameof(filePath), format, null),
        };
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
