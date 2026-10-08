using Goro.Domain;
using Goro.Hashing;
using Goro.Reading.Bytes;

namespace Goro.Pipeline;

public sealed class PipelinePlanner(IAudioHasher audioHasher, ReadPolicy? policy = null) : IPipelinePlanner
{
    public IPipelineStage<string, FileOutcome<HashResult>> PlanHash(HashOptions options) =>
        new HashPipelineStage(audioHasher, options.Algorithm);

    public IPipelineStage<string, FileOutcome<ListResult>> PlanList(ListOptions options) =>
        options.Filter is { } filter ? new PredicateStage(filter, policy) : new ListPipelineStage();
}
