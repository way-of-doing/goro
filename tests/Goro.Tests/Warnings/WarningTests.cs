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
    public void FileWarning_From_DescribesCommonCausesWithoutRepeatingThePath()
    {
        Assert.That(FileWarning.From("/a.mp3", new FileNotFoundException("Could not find file '/a.mp3'.")).Cause, Is.EqualTo("no such file or directory"));
        Assert.That(FileWarning.From("/d", new DirectoryNotFoundException("Could not find a part of the path '/d'.")).Cause, Is.EqualTo("no such file or directory"));
        Assert.That(FileWarning.From("/a.mp3", new UnauthorizedAccessException("Access to the path '/a.mp3' is denied.")).Cause, Is.EqualTo("permission denied"));
        Assert.That(FileWarning.From("/a.mp3", new IOException("Input/output error")).Cause, Is.EqualTo("Input/output error"));
    }
}
