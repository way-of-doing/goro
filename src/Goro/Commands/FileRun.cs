using Goro.Cli;
using Goro.Discovery;
using Goro.Execution;
using Goro.Output;
using Goro.Pipeline;
using Goro.Warnings;

namespace Goro.Commands;

/// <summary>
/// The shape every file-processing command's run takes, once its options have been read: resolve
/// the pathspecs, discover, run the pipeline over each file, render, and choose the exit code from
/// what the run found.
/// </summary>
internal sealed class FileRun(IFileDiscoveryService fileDiscovery, IExecutor executor)
{
    public async Task<int> ExecuteAsync<TResult>(
        GlobalSettings settings,
        IReadOnlyList<string> pathSpecs,
        Func<IPipelineStage<string, FileOutcome<TResult>>> plan,
        IOutputRenderer<TResult> renderer,
        CancellationToken cancellationToken)
        where TResult : class
    {
        // Every pathspec is resolved before anything is planned or written, so that a rejected
        // one leaves no output at all. See docs/concepts/pathspecs.md.
        ResolvedPathSpecs resolved;
        try
        {
            resolved = fileDiscovery.Resolve(pathSpecs);
        }
        catch (PathSpecException ex)
        {
            await Console.Error.WriteLineAsync($"goro: error: {ex.Message}");
            return ExitCodes.Rejected;
        }

        var warnings = new WarningSink(Console.Error, settings.SuppressedWarnings);
        var tally = new RunTally(warnings);

        var pipeline = plan();
        var files = fileDiscovery.DiscoverAsync(resolved, warnings, cancellationToken);
        var results = executor.ExecuteAsync(pipeline, files, tally, cancellationToken);

        await renderer.RenderAsync(results, Console.Out, cancellationToken);
        return ExitCodes.For(tally.Outcome, settings.StrictExitCode);
    }
}
