using Goro.Output;
using Goro.Tests.TestSupport;

namespace Goro.Tests.Output;

public class OutputFormatExtensionsTests
{
    private sealed record SampleResult(string Value);

    [TestCase("plain", OutputFormat.Plain)]
    [TestCase("PLAIN", OutputFormat.Plain)]
    [TestCase("json", OutputFormat.Json)]
    [TestCase("JSON", OutputFormat.Json)]
    public void TryParse_KnownNamesCaseInsensitive_ReturnsExpectedValue(string value, OutputFormat expected)
    {
        var success = OutputFormatExtensions.TryParse(value, out var format);

        Assert.That(success, Is.True);
        Assert.That(format, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void TryParse_NullOrEmpty_DefaultsToPlain(string? value)
    {
        var success = OutputFormatExtensions.TryParse(value, out var format);

        Assert.That(success, Is.True);
        Assert.That(format, Is.EqualTo(OutputFormat.Plain));
    }

    [Test]
    public void TryParse_UnknownValue_ReturnsFalse()
    {
        var success = OutputFormatExtensions.TryParse("yaml", out _);

        Assert.That(success, Is.False);
    }

    [Test]
    public void ValidNames_ListsExactlyTheSupportedFormats()
    {
        Assert.That(OutputFormatExtensions.ValidNames, Is.EqualTo(new[] { "plain", "json" }));
    }

    [Test]
    public async Task CreateRenderer_Plain_UsesTheGivenLineFormatter()
    {
        var renderer = OutputFormat.Plain.CreateRenderer<SampleResult>(r => $"<{r.Value}>");
        using var writer = new StringWriter();

        await renderer.RenderAsync(new[] { new SampleResult("x") }.AsAsyncEnumerable(), writer, CancellationToken.None);

        Assert.That(writer.ToString().Trim(), Is.EqualTo("<x>"));
    }

    [Test]
    public async Task CreateRenderer_Json_ProducesJsonArray()
    {
        var renderer = OutputFormat.Json.CreateRenderer<SampleResult>(r => r.Value);
        using var writer = new StringWriter();

        await renderer.RenderAsync(new[] { new SampleResult("x") }.AsAsyncEnumerable(), writer, CancellationToken.None);

        var output = writer.ToString();
        Assert.That(output.TrimStart(), Does.StartWith("["));
        Assert.That(output, Does.Contain("\"Value\""));
    }
}
