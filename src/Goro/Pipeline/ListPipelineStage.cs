using Goro.Domain;

namespace Goro.Pipeline;

public sealed class ListPipelineStage : IPipelineStage<string, ListResult>
{
    public Task<ListResult> ExecuteAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult(new ListResult(filePath));
}
