using Goro.Domain;
using Goro.Hashing;

namespace Goro.Pipeline;

public sealed class PipelinePlanner(IAudioHasher audioHasher) : IPipelinePlanner
{
    public IPipelineStage<string, HashResult> PlanHash(HashOptions options) =>
        new HashPipelineStage(audioHasher, options.Algorithm);

    public IPipelineStage<string, ListResult> PlanList(ListOptions options) =>
        new ListPipelineStage();
}
