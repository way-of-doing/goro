using Goro.Cli;
using Goro.Commands;
using Goro.Discovery;
using Goro.Execution;
using Goro.Hashing;
using Goro.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
services.AddSingleton<IAudioHasher, TagLibAudioHasher>();
services.AddSingleton<IPipelinePlanner, PipelinePlanner>();
services.AddSingleton<IExecutor, ConcurrentExecutor>();
services.AddTransient<HashCommand>();
services.AddTransient<ListCommand>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.AddCommand<HashCommand>("hash");
    config.AddCommand<ListCommand>("list");
});

return await app.RunAsync(args, CancellationToken.None);
