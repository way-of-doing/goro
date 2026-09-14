namespace Goro.Output;

public sealed class PlainOutputRenderer<TResult>(Func<TResult, string> formatLine) : IOutputRenderer<TResult>
{
    public async Task RenderAsync(IAsyncEnumerable<TResult> results, TextWriter writer, CancellationToken cancellationToken)
    {
        await foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(formatLine(result));
        }
    }
}
