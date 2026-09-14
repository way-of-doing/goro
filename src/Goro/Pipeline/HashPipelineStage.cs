using Goro.Domain;
using Goro.Hashing;

namespace Goro.Pipeline;

public sealed class HashPipelineStage(IAudioHasher audioHasher, HashAlgorithmKind algorithm) : IPipelineStage<string, HashResult>
{
    public async Task<HashResult> ExecuteAsync(string filePath, CancellationToken cancellationToken)
    {
        var hash = await audioHasher.ComputeHashAsync(filePath, algorithm, cancellationToken);
        return new HashResult(filePath, algorithm.ToName(), hash);
    }
}
