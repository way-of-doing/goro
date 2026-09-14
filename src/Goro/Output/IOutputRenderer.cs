namespace Goro.Output;

/// <summary>
/// Consumes a stream of result objects produced by the executor and writes output.
/// </summary>
public interface IOutputRenderer<TResult>
{
    Task RenderAsync(IAsyncEnumerable<TResult> results, TextWriter writer, CancellationToken cancellationToken);
}
