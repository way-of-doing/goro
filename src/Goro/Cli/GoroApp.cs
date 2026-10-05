using Goro.Commands;
using Goro.Discovery;
using Goro.Execution;
using Goro.Hashing;
using Goro.Pipeline;
using Goro.Predicates.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Goro.Cli;

/// <summary>
/// The composition root: the services Goro is built from, the commands it understands, and how a
/// command line is read and its failures reported. <c>Program.cs</c> runs it as it is; the
/// integration tests run the same thing with some services replaced.
/// </summary>
public sealed class GoroApp
{
    private readonly CommandApp _app;

    public GoroApp(IServiceCollection services)
    {
        _app = new CommandApp(new TypeRegistrar(services));
        _app.Configure(config =>
        {
            config.AddCommand<HashCommand>("hash");
            config.AddCommand<ListCommand>("list");

            // An option Goro does not know is a mistake in the invocation, not something to skip
            // over: without this, Spectre accepts it silently and takes the next argument as its
            // value, so a misspelt option would quietly eat a pathspec.
            config.UseStrictParsing();
            config.SetExceptionHandler(HandleException);
        });
    }

    /// <summary>The services Goro runs with, which a caller may add to or replace before building the app.</summary>
    public static IServiceCollection DefaultServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
        services.AddSingleton<IAudioHasher, TagLibAudioHasher>();
        services.AddSingleton<IPipelinePlanner, PipelinePlanner>();
        services.AddSingleton<IExecutor, ConcurrentExecutor>();
        services.AddSingleton<IIdentifierCatalog>(BuiltInCatalog.Instance);
        services.AddTransient<HashCommand>();
        services.AddTransient<ListCommand>();
        return services;
    }

    public Task<int> RunAsync(IEnumerable<string> args, CancellationToken cancellationToken) =>
        _app.RunAsync(NoWarnOption.AttachValues(FilterOption.AttachValue(args)), cancellationToken);

    // Spectre reports a command line it cannot accept as a CommandAppException, by default with
    // exit code -1. docs/concepts/exit-codes.md gives a rejected command line 2; anything else that
    // escapes a command means the run started and could not be completed, which is 1.
    private static int HandleException(Exception exception, ITypeResolver? resolver)
    {
        // Created per call, rather than using the static AnsiConsole, so that it writes to whatever
        // standard error is at the time.
        var error = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });

        if (exception is CommandAppException rejected)
        {
            if (rejected.Pretty is { } pretty)
            {
                error.Write(pretty);
            }
            else
            {
                error.WriteLine($"Error: {rejected.Message}", Style.Plain);
            }

            return ExitCodes.Rejected;
        }

        error.WriteLine($"goro: error: {exception.Message}", Style.Plain);
        return ExitCodes.Failed;
    }
}
