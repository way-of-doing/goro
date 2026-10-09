using Goro.Cli;
using Goro.Discovery;
using Goro.Execution;
using Goro.Messages;
using Goro.Output;
using Goro.Pipeline;
using Goro.Warnings;

namespace Goro.Commands;

/// <summary>
/// The shape every file-processing command's run takes, once its options have been read: resolve
/// the pathspecs, discover, run the pipeline over each file, render, report any predicates that could
/// not be answered and any files read only in part, and choose the exit code from what the run found.
/// </summary>
internal sealed class FileRun(IFileDiscoveryService fileDiscovery, IExecutor executor, IErrorMessages messages)
{
    public async Task<int> ExecuteAsync<TResult>(
        GlobalSettings settings,
        IReadOnlyList<string> pathSpecs,
        Func<IPipelineStage<string, FileOutcome<TResult>>> plan,
        IOutputRenderer<TResult> renderer,
        CancellationToken cancellationToken,
        Func<int, int, Warning>? unanswered = null)
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
            await Console.Error.WriteLineAsync($"{messages.ErrorPrefix}{messages.Render(ex.Error)}");
            return ExitCodes.Rejected;
        }

        var warnings = new WarningSink(Console.Error, settings.SuppressedWarnings);
        var tally = new RunTally(warnings);

        var pipeline = plan();
        var files = fileDiscovery.DiscoverAsync(resolved, warnings, cancellationToken);
        var results = executor.ExecuteAsync(pipeline, files, tally, cancellationToken);

        await renderer.RenderAsync(results, Console.Out, cancellationToken);

        // Once every file has been processed, so that they come after every other warning, and never
        // for a run that was interrupted, since an interrupted run does not get this far. They come
        // in the order of their exit codes. The sink suppresses them like any other, which is what
        // keeps their codes out of the outcome too.
        var counted = tally.Outcome;
        if (counted.Unanswered > 0 && unanswered is not null)
        {
            warnings.Emit([unanswered(counted.Unanswered, counted.Examined)]);
        }

        if (counted.Incomplete > 0)
        {
            warnings.Emit([new IncompleteWarning(counted.Incomplete, counted.Opened)]);
        }

        return ExitCodes.For(tally.Outcome, settings.StrictExitCode);
    }
}
