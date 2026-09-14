using Goro.Domain;

namespace Goro.Pipeline;

/// <summary>
/// Reads structured invocation data (per-command options) and constructs the async
/// execution pipeline for that invocation.
/// </summary>
public interface IPipelinePlanner
{
    IPipelineStage<string, HashResult> PlanHash(HashOptions options);

    IPipelineStage<string, ListResult> PlanList(ListOptions options);
}
