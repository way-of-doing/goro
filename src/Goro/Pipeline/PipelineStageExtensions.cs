namespace Goro.Pipeline;

public static class PipelineStageExtensions
{
    /// <summary>
    /// Sequences two stages into one: runs <paramref name="first"/>, then feeds its
    /// output into <paramref name="second"/> as input.
    /// </summary>
    public static IPipelineStage<TInput, TNext> Then<TInput, TOutput, TNext>(
        this IPipelineStage<TInput, TOutput> first,
        IPipelineStage<TOutput, TNext> second) =>
        new ComposedPipelineStage<TInput, TOutput, TNext>(first, second);

    private sealed class ComposedPipelineStage<TInput, TOutput, TNext>(
        IPipelineStage<TInput, TOutput> first,
        IPipelineStage<TOutput, TNext> second) : IPipelineStage<TInput, TNext>
    {
        public async Task<TNext> ExecuteAsync(TInput input, CancellationToken cancellationToken)
        {
            var intermediate = await first.ExecuteAsync(input, cancellationToken);
            return await second.ExecuteAsync(intermediate, cancellationToken);
        }
    }
}
