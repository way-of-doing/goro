using Goro.Cli;

namespace Goro.Tests.Cli;

public class FilterOptionTests
{
    [Test]
    public void ASeparateValue_IsAttached()
    {
        Assert.That(FilterOption.AttachValue(["list", "--filter", "a == 1", "/music"]),
            Is.EqualTo(new[] { "list", "--filter=a == 1", "/music" }));
    }

    // The reason the rewrite exists: Spectre would otherwise read this value as more options.
    [Test]
    public void ASeparateValueBeginningWithADash_IsAttachedWhole()
    {
        Assert.That(FilterOption.AttachValue(["list", "--filter", "-1 < x"]), Is.EqualTo(new[] { "list", "--filter=-1 < x" }));
    }

    [Test]
    public void AnAttachedValue_IsLeftAlone()
    {
        Assert.That(FilterOption.AttachValue(["list", "--filter=a == 1"]), Is.EqualTo(new[] { "list", "--filter=a == 1" }));
    }

    [Test]
    public void AnEmptySeparateValue_IsLeftSeparate_SoThatItIsReportedAsAnEmptyPredicate()
    {
        Assert.That(FilterOption.AttachValue(["list", "--filter", ""]), Is.EqualTo(new[] { "list", "--filter", "" }));
    }

    [Test]
    public void TheOptionLast_IsLeftForSpectreToReject()
    {
        Assert.That(FilterOption.AttachValue(["list", "--filter"]), Is.EqualTo(new[] { "list", "--filter" }));
    }

    [Test]
    public void AfterTheSeparator_NothingIsTouched()
    {
        Assert.That(FilterOption.AttachValue(["list", "--", "--filter", "x"]), Is.EqualTo(new[] { "list", "--", "--filter", "x" }));
    }

    [Test]
    public void TheValueItself_IsNeverReadAsTheSeparatorOrAsAnotherOption()
    {
        Assert.That(FilterOption.AttachValue(["list", "--filter", "--", "--filter", "x"]),
            Is.EqualTo(new[] { "list", "--filter=--", "--filter=x" }));
    }
}
