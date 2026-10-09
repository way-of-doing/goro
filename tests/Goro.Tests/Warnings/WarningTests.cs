using Goro.Warnings;

namespace Goro.Tests.Warnings;

public class WarningTests
{
    [Test]
    public void DataWarning_NamesTheFileAndQuotesTheSubExpressionAsWritten()
    {
        var warning = new DataWarning("/music/a.mp3", "number(ID3V2::TRCK)");

        Assert.That(warning.Category, Is.EqualTo(WarningCategory.Data));
        Assert.That(warning.ToString(), Is.EqualTo("goro: warning: /music/a.mp3: cannot interpret the data of number(ID3V2::TRCK)"));
    }

    [Test]
    public void FileWarning_NamesTheFileAndTheCause()
    {
        var warning = new FileWarning("/music/a.mp3", "MPEG audio header not found.");

        Assert.That(warning.Category, Is.EqualTo(WarningCategory.File));
        Assert.That(warning.ToString(), Is.EqualTo("goro: warning: /music/a.mp3: cannot be read: MPEG audio header not found."));
    }

    [Test]
    public void UnansweredWarning_NamesNoPath_SayingWhatTheCommandWroteWhole()
    {
        var warning = new UnansweredWarning("the predicate could not be answered for the one file examined, which was not listed");

        Assert.That(warning.Category, Is.EqualTo(WarningCategory.Unanswered));
        Assert.That(warning.ToString(), Is.EqualTo("goro: warning: the predicate could not be answered for the one file examined, which was not listed"));
    }

    [TestCase(1, 1, "goro: warning: the one file opened could be read only in part; goro audit can say what it holds")]
    [TestCase(1, 40, "goro: warning: 1 of the 40 files opened could be read only in part; goro audit can say what it holds")]
    [TestCase(3, 3, "goro: warning: none of the 3 files opened could be read in full; goro audit can say what they hold")]
    [TestCase(3, 40, "goro: warning: 3 of the 40 files opened could be read only in part; goro audit can say what they hold")]
    public void IncompleteWarning_NamesNoPath_AndCountsTheFilesInWholeSentences(int incomplete, int opened, string expected)
    {
        var warning = new IncompleteWarning(incomplete, opened);

        Assert.That(warning.Category, Is.EqualTo(WarningCategory.Incomplete));
        Assert.That(warning.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void FileWarning_From_DescribesCommonCausesWithoutRepeatingThePath()
    {
        Assert.That(FileWarning.From("/a.mp3", new FileNotFoundException("Could not find file '/a.mp3'.")).Cause, Is.EqualTo("no such file or directory"));
        Assert.That(FileWarning.From("/d", new DirectoryNotFoundException("Could not find a part of the path '/d'.")).Cause, Is.EqualTo("no such file or directory"));
        Assert.That(FileWarning.From("/a.mp3", new UnauthorizedAccessException("Access to the path '/a.mp3' is denied.")).Cause, Is.EqualTo("permission denied"));
        Assert.That(FileWarning.From("/a.mp3", new IOException("Input/output error")).Cause, Is.EqualTo("Input/output error"));
    }
}
