using Goro.Discovery;

namespace Goro.Tests.Discovery;

public class CandidateFilesTests
{
    // The cases docs/testing.md gives for file::extension.
    [TestCase("a.flac", "flac")]
    [TestCase("a.tar.gz", "gz")]
    [TestCase(".hidden", null)]
    [TestCase("README", null)]
    [TestCase("trailing.", null)]
    public void Extension_FollowsTheFileExtensionRule(string fileName, string? expected)
    {
        Assert.That(CandidateFiles.Extension(fileName), Is.EqualTo(expected));
    }

    [TestCase("/music/a.mp3", true)]
    [TestCase("/music/a.MP3", true)]
    [TestCase("/music/a.flac", false)]
    [TestCase("/music/.mp3", false)]
    [TestCase("/music.mp3/cover", false)]
    public void IsCandidate_JudgesTheFileNameByItsExtension(string path, bool expected)
    {
        Assert.That(CandidateFiles.IsCandidate(path), Is.EqualTo(expected));
    }
}
