using Goro.Cli;

namespace Goro.Tests.Cli;

public class NoWarnOptionTests
{
    [TestCase(new[] { "list", "--no-warn", "/music" }, new[] { "list", "--no-warn=all", "/music" })]
    [TestCase(new[] { "list", "/music", "--no-warn" }, new[] { "list", "/music", "--no-warn=all" })]
    [TestCase(new[] { "list", "--no-warn=data", "/music" }, new[] { "list", "--no-warn=data", "/music" })]
    [TestCase(new[] { "list", "--", "--no-warn" }, new[] { "list", "--", "--no-warn" })]
    [TestCase(new[] { "list", "--no-warn", "--", "--no-warn" }, new[] { "list", "--no-warn=all", "--", "--no-warn" })]
    [TestCase(new[] { "list", "--no-warning", "/music" }, new[] { "list", "--no-warning", "/music" })]
    public void AttachValues_RewritesOnlyTheBareOptionBeforeTheSeparator(string[] args, string[] expected)
    {
        Assert.That(NoWarnOption.AttachValues(args), Is.EqualTo(expected));
    }
}
