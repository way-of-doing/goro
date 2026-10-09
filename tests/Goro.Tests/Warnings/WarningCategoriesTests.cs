using Goro.Warnings;

namespace Goro.Tests.Warnings;

public class WarningCategoriesTests
{
    private const WarningCategory Data = WarningCategory.Data;
    private const WarningCategory Unanswered = WarningCategory.Unanswered;
    private const WarningCategory Incomplete = WarningCategory.Incomplete;
    private const WarningCategory File = WarningCategory.File;

    private static IEnumerable<TestCaseData> Accepted()
    {
        yield return new TestCaseData(null, new[] { Data, Unanswered, Incomplete, File }).SetName("bare option means every category");
        yield return new TestCaseData("", new[] { Data, Unanswered, Incomplete, File }).SetName("empty value means every category");
        yield return new TestCaseData("all", new[] { Data, Unanswered, Incomplete, File });
        yield return new TestCaseData("data", new[] { Data });
        yield return new TestCaseData("file", new[] { File });
        yield return new TestCaseData("unanswered", new[] { Unanswered });
        yield return new TestCaseData("incomplete", new[] { Incomplete });
        yield return new TestCaseData("Incomplete,file", new[] { Incomplete, File }).SetName("names are case-insensitive (Incomplete,file)");
        yield return new TestCaseData("data,unanswered,incomplete,file", new[] { Data, Unanswered, Incomplete, File }).SetName("every name listed is all");
        yield return new TestCaseData("data,unanswered", new[] { Data, Unanswered });
        yield return new TestCaseData("data,file", new[] { Data, File });
        yield return new TestCaseData("file,data", new[] { Data, File });
        yield return new TestCaseData("data,data", new[] { Data });
        yield return new TestCaseData("data,all", new[] { Data, Unanswered, Incomplete, File });
        yield return new TestCaseData("DATA", new[] { Data }).SetName("names are case-insensitive (DATA)");
        yield return new TestCaseData("File,ALL", new[] { Data, Unanswered, Incomplete, File }).SetName("names are case-insensitive (File,ALL)");
        yield return new TestCaseData(" data , file ", new[] { Data, File }).SetName("spaces around names are ignored");
    }

    [TestCaseSource(nameof(Accepted))]
    public void TryParse_AcceptedList_GivesItsCategories(string? list, WarningCategory[] expected)
    {
        var parsed = WarningCategories.TryParse(list, out var categories, out var unknown);

        Assert.That(parsed, Is.True);
        Assert.That(categories, Is.EquivalentTo(expected));
        Assert.That(unknown, Is.Null);
    }

    [TestCase("tag", "tag")]
    [TestCase("data,tag", "tag")]
    [TestCase("data,", "")]
    [TestCase("datas", "datas")]
    [TestCase("none", "none")]
    public void TryParse_UnknownName_IsRejectedAndNamed(string list, string expectedUnknown)
    {
        var parsed = WarningCategories.TryParse(list, out _, out var unknown);

        Assert.That(parsed, Is.False);
        Assert.That(unknown, Is.EqualTo(expectedUnknown));
    }
}
