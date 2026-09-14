using Goro.Domain;
using Goro.Output;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Output;

public class JsonOutputRendererTests
{
    [Test]
    public async Task RenderAsync_HashResult_MatchesDocumentedJsonShape()
    {
        var renderer = new JsonOutputRenderer<HashResult>();
        var results = new[]
        {
            new HashResult("/absolute/path/to/audio.mp3", "md5", "0123456789abcdef0123456789abcdef"),
        }.AsAsyncEnumerable();

        using var writer = new StringWriter();
        await renderer.RenderAsync(results, writer, CancellationToken.None);

        var expected = string.Join(
            "\n",
            "[",
            "  {",
            "    \"file\": \"/absolute/path/to/audio.mp3\",",
            "    \"algo\": \"md5\",",
            "    \"hash\": \"0123456789abcdef0123456789abcdef\"",
            "  }",
            "]",
            string.Empty);

        Assert.That(writer.ToString().Replace("\r\n", "\n"), Is.EqualTo(expected));
    }

    [Test]
    public async Task RenderAsync_ListResult_MatchesDocumentedJsonShape()
    {
        var renderer = new JsonOutputRenderer<ListResult>();
        var results = new[] { new ListResult("/absolute/path/to/audio.mp3") }.AsAsyncEnumerable();

        using var writer = new StringWriter();
        await renderer.RenderAsync(results, writer, CancellationToken.None);

        var expected = string.Join(
            "\n",
            "[",
            "  {",
            "    \"file\": \"/absolute/path/to/audio.mp3\"",
            "  }",
            "]",
            string.Empty);

        Assert.That(writer.ToString().Replace("\r\n", "\n"), Is.EqualTo(expected));
    }
}
