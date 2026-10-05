using Goro.Domain;

namespace Goro.Pipeline;

/// <summary>
/// Lists every file it is given. It reads nothing, so no file can fail to be read here.
/// </summary>
public sealed class ListPipelineStage : IPipelineStage<string, FileOutcome<ListResult>>
{
    public Task<FileOutcome<ListResult>> ExecuteAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult(FileOutcome<ListResult>.Matched(new ListResult(filePath)));
}
