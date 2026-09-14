using Goro.Pipeline;

namespace Goro.Tests.Pipeline;

public class PipelineStageExtensionsTests
{
    private sealed class DelegateStage<TInput, TOutput>(Func<TInput, TOutput> transform) : IPipelineStage<TInput, TOutput>
    {
        public Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken) => Task.FromResult(transform(input));
    }

    [Test]
    public async Task Then_ComposesTwoStages_FeedingTheFirstsOutputIntoTheSecond()
    {
        var stringLength = new DelegateStage<string, int>(s => s.Length);
        var describe = new DelegateStage<int, string>(n => $"length={n}");

        var composed = stringLength.Then(describe);
        var result = await composed.ExecuteAsync("hello", CancellationToken.None);

        Assert.That(result, Is.EqualTo("length=5"));
    }

    [Test]
    public async Task Then_SecondStageReceivesTheFirstsOutput_NotTheOriginalInput()
    {
        var seenByFirst = new List<string>();
        var seenBySecond = new List<int>();

        var first = new DelegateStage<string, int>(s =>
        {
            seenByFirst.Add(s);
            return s.Length;
        });
        var second = new DelegateStage<int, int>(n =>
        {
            seenBySecond.Add(n);
            return n * 2;
        });

        var composed = first.Then(second);
        var result = await composed.ExecuteAsync("abc", CancellationToken.None);

        Assert.That(seenByFirst, Is.EqualTo(new[] { "abc" }));
        Assert.That(seenBySecond, Is.EqualTo(new[] { 3 }));
        Assert.That(result, Is.EqualTo(6));
    }
}
