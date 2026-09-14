namespace Goro.Pipeline;

/// <summary>
/// A composable per-item operation: takes one input and produces one output.
/// Can be invoked concurrently an arbitrary number of times (see <see cref="Goro.Execution.IExecutor"/>).
/// Stages compose via <see cref="PipelineStageExtensions.Then{TInput,TOutput,TNext}"/>,
/// so e.g. a future "verify" stage can be built by feeding a hash stage's output
/// into a comparison stage, rather than duplicating the hashing logic.
/// </summary>
public interface IPipelineStage<in TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
