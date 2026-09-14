using Goro.Domain;
using Goro.Output;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Output;

public class PlainOutputRendererTests
{
    [Test]
    public async Task RenderAsync_HashResult_MatchesDocumentedPlainFormat()
    {
        var renderer = new PlainOutputRenderer<HashResult>(r => $"{r.File} {r.Algo} {r.Hash}");
        var results = new[]
        {
            new HashResult("/absolute/path/to/audio.mp3", "md5", "0123456789abcdef0123456789abcdef"),
        }.AsAsyncEnumerable();

        using var writer = new StringWriter();
        await renderer.RenderAsync(results, writer, CancellationToken.None);

        Assert.That(
            writer.ToString().Replace("\r\n", "\n"),
            Is.EqualTo("/absolute/path/to/audio.mp3 md5 0123456789abcdef0123456789abcdef\n"));
    }

    [Test]
    public async Task RenderAsync_ListResult_MatchesDocumentedPlainFormat()
    {
        var renderer = new PlainOutputRenderer<ListResult>(r => r.File);
        var results = new[] { new ListResult("/absolute/path/to/audio.mp3") }.AsAsyncEnumerable();

        using var writer = new StringWriter();
        await renderer.RenderAsync(results, writer, CancellationToken.None);

        Assert.That(writer.ToString().Replace("\r\n", "\n"), Is.EqualTo("/absolute/path/to/audio.mp3\n"));
    }
}
