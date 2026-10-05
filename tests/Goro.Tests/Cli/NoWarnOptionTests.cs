using Goro.Cli;

namespace Goro.Tests.Cli;

public class NoWarnOptionTests
{
    [TestCase(new[] { "list", "/music", "--no-warn" }, new[] { "list", "/music", "--no-warn", "" })]
    [TestCase(new[] { "list", "--no-warn", "--strict-exit-code", "/music" }, new[] { "list", "--no-warn", "", "--strict-exit-code", "/music" })]
    [TestCase(new[] { "list", "--no-warn", "--", "/music" }, new[] { "list", "--no-warn", "", "--", "/music" })]
    [TestCase(new[] { "list", "--no-warn", "data", "/music" }, new[] { "list", "--no-warn", "data", "/music" })]
    [TestCase(new[] { "list", "--no-warn=data", "/music" }, new[] { "list", "--no-warn=data", "/music" })]
    [TestCase(new[] { "list", "--", "--no-warn" }, new[] { "list", "--", "--no-warn" })]
    [TestCase(new[] { "list", "--no-warning", "/music" }, new[] { "list", "--no-warning", "/music" })]
    public void MarkMissingValue_GivesOnlyAValuelessOptionBeforeTheSeparatorAnEmptyValue(string[] args, string[] expected)
    {
        Assert.That(NoWarnOption.MarkMissingValue(args), Is.EqualTo(expected));
    }
}
