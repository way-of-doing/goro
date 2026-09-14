using Goro.Domain;
using Goro.Hashing;
using Goro.Output;
using Goro.Pipeline;

namespace Goro.Tests.Pipeline;

public class PipelinePlannerTests
{
    private sealed class FakeAudioHasher : IAudioHasher
    {
        public string? LastFilePath { get; private set; }

        public HashAlgorithmKind? LastAlgorithm { get; private set; }

        public Task<string> ComputeHashAsync(string filePath, HashAlgorithmKind algorithm, CancellationToken cancellationToken)
        {
            LastFilePath = filePath;
            LastAlgorithm = algorithm;
            return Task.FromResult("deadbeef");
        }
    }

    [Test]
    public async Task PlanHash_ReturnsStageThatUsesTheConfiguredAlgorithm()
    {
        var hasher = new FakeAudioHasher();
        var planner = new PipelinePlanner(hasher);
        var options = new HashOptions(["."], HashAlgorithmKind.Sha1, OutputFormat.Plain);

        var stage = planner.PlanHash(options);
        var result = await stage.ExecuteAsync("/music/track.mp3", CancellationToken.None);

        Assert.That(hasher.LastFilePath, Is.EqualTo("/music/track.mp3"));
        Assert.That(hasher.LastAlgorithm, Is.EqualTo(HashAlgorithmKind.Sha1));
        Assert.That(result, Is.EqualTo(new HashResult("/music/track.mp3", "sha1", "deadbeef")));
    }

    [Test]
    public async Task PlanList_ReturnsStageThatWrapsTheFilePath()
    {
        var planner = new PipelinePlanner(new FakeAudioHasher());
        var options = new ListOptions(["."], OutputFormat.Plain);

        var stage = planner.PlanList(options);
        var result = await stage.ExecuteAsync("/music/track.mp3", CancellationToken.None);

        Assert.That(result, Is.EqualTo(new ListResult("/music/track.mp3")));
    }
}
