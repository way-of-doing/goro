using Goro.Cli;
using Goro.Commands;
using Goro.Discovery;
using Goro.Execution;
using Goro.Hashing;
using Goro.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Goro.Tests.TestSupport;

/// <summary>
/// Builds a <see cref="CommandApp"/> wired the same way as the real <c>Program.cs</c>
/// composition root, for CLI-layer integration tests.
/// </summary>
internal static class GoroAppFactory
{
    public static CommandApp Create()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
        services.AddSingleton<IAudioHasher, TagLibAudioHasher>();
        services.AddSingleton<IPipelinePlanner, PipelinePlanner>();
        services.AddSingleton<IExecutor, ConcurrentExecutor>();
        services.AddTransient<HashCommand>();
        services.AddTransient<ListCommand>();

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(config =>
        {
            config.AddCommand<HashCommand>("hash");
            config.AddCommand<ListCommand>("list");
        });

        return app;
    }

    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunCapturedAsync(this CommandApp app, params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        // Deliberately not disposed: Spectre.Console.Cli caches its internal AnsiConsole
        // output backend statically the first time it renders something (e.g. a
        // validation error), bound to whatever Console.Out/Error was active at that
        // moment. Disposing these StringWriters would flip them into a "closed" state,
        // so a later test that reuses that stale cached reference would throw
        // ObjectDisposedException on write instead of harmlessly writing to a
        // StringBuilder nobody reads anymore.
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            var exitCode = await app.RunAsync(args, CancellationToken.None);
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
