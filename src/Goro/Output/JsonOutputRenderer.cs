using System.Text.Json;

namespace Goro.Output;

public sealed class JsonOutputRenderer<TResult> : IOutputRenderer<TResult>
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task RenderAsync(IAsyncEnumerable<TResult> results, TextWriter writer, CancellationToken cancellationToken)
    {
        var collected = new List<TResult>();
        await foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            collected.Add(result);
        }

        var json = JsonSerializer.Serialize(collected, SerializerOptions);
        await writer.WriteLineAsync(json);
    }
}
