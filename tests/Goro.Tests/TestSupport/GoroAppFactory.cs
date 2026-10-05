using Goro.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace Goro.Tests.TestSupport;

/// <summary>
/// Builds the same <see cref="GoroApp"/> as the real <c>Program.cs</c>, for CLI-layer integration
/// tests, optionally with some of its services replaced.
/// </summary>
internal static class GoroAppFactory
{
    /// <param name="configure">
    /// Adds registrations after Goro's own, which therefore replace them, e.g. a discovery service
    /// that yields a file which no longer exists.
    /// </param>
    public static GoroApp Create(Action<IServiceCollection>? configure = null)
    {
        var services = GoroApp.DefaultServices();
        configure?.Invoke(services);
        return new GoroApp(services);
    }

    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunCapturedAsync(this GoroApp app, params string[] args)
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
