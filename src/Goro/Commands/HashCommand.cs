using Goro.Cli;
using Goro.Discovery;
using Goro.Domain;
using Goro.Execution;
using Goro.Hashing;
using Goro.Output;
using Goro.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Goro.Commands;

public sealed class HashCommand(IFileDiscoveryService fileDiscovery, IPipelinePlanner pipelinePlanner, IExecutor executor)
    : AsyncCommand<HashCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[pathspecs]")]
        public string[] PathSpecs { get; set; } = [];

        [CommandOption("-a|--algo|--algorithm <ALGORITHM>")]
        public string? Algorithm { get; set; }

        [CommandOption("-o|--output <FORMAT>")]
        public string? Output { get; set; }

        public override ValidationResult Validate()
        {
            if (!HashAlgorithmKindExtensions.TryParse(Algorithm, out _))
            {
                return ValidationResult.Error($"Invalid algorithm '{Algorithm}'. Valid values: {string.Join(", ", HashAlgorithmKindExtensions.ValidNames)}.");
            }

            if (!OutputFormatExtensions.TryParse(Output, out _))
            {
                return ValidationResult.Error($"Invalid output format '{Output}'. Valid values: {string.Join(", ", OutputFormatExtensions.ValidNames)}.");
            }

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        HashAlgorithmKindExtensions.TryParse(settings.Algorithm, out var algorithm);
        OutputFormatExtensions.TryParse(settings.Output, out var output);

        var pathSpecs = OptionParsing.ResolvePathSpecs(settings.PathSpecs, context.Remaining.Raw);
        var options = new HashOptions(pathSpecs, algorithm, output);

        var pipeline = pipelinePlanner.PlanHash(options);
        var files = fileDiscovery.DiscoverAsync(options.PathSpecs, cancellationToken);
        var results = executor.ExecuteAsync(pipeline, files, cancellationToken);

        var renderer = output.CreateRenderer<HashResult>(r => $"{r.File} {r.Algo} {r.Hash}");

        await renderer.RenderAsync(results, Console.Out, cancellationToken);
        return 0;
    }
}
