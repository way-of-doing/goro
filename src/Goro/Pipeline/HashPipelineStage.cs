using Goro.Domain;
using Goro.Hashing;
using Goro.Warnings;

namespace Goro.Pipeline;

/// <summary>
/// Hashes one file's audio. A file whose audio cannot be read still has a row in the output, with
/// its hash absent, and one file warning (docs/commands/hash.md).
/// </summary>
public sealed class HashPipelineStage(IAudioHasher audioHasher, HashAlgorithmKind algorithm) : IPipelineStage<string, FileOutcome<HashResult>>
{
    public async Task<FileOutcome<HashResult>> ExecuteAsync(string filePath, CancellationToken cancellationToken)
    {
        var algo = algorithm.ToName();
        try
        {
            var hash = await audioHasher.ComputeHashAsync(filePath, algorithm, cancellationToken);
            return FileOutcome<HashResult>.Matched(new HashResult(filePath, algo, hash), reading: FileReading.Read);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Reading the file is all the hasher does, so anything it throws means the file cannot
            // be read: gone, refused, damaged, or not in a form Goro understands. TagLibSharp is
            // known to throw more than its own exception types on malformed input, so this does not
            // try to enumerate them.
            return FileOutcome<HashResult>.Unreadable(FileWarning.From(filePath, ex), new HashResult(filePath, algo, null));
        }
    }
}
