using Goro.Cli;
using Goro.Discovery;
using Goro.Domain;
using Goro.Execution;
using Goro.Output;
using Goro.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Goro.Commands;

public sealed class ListCommand(IFileDiscoveryService fileDiscovery, IPipelinePlanner pipelinePlanner, IExecutor executor)
    : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[pathspecs]")]
        public string[] PathSpecs { get; set; } = [];

        [CommandOption("-o|--output <FORMAT>")]
        public string? Output { get; set; }

        public override ValidationResult Validate()
        {
            if (!OutputFormatExtensions.TryParse(Output, out _))
            {
                return ValidationResult.Error($"Invalid output format '{Output}'. Valid values: {string.Join(", ", OutputFormatExtensions.ValidNames)}.");
            }

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        OutputFormatExtensions.TryParse(settings.Output, out var output);

        var pathSpecs = OptionParsing.ResolvePathSpecs(settings.PathSpecs, context.Remaining.Raw);
        var options = new ListOptions(pathSpecs, output);

        var pipeline = pipelinePlanner.PlanList(options);
        var files = fileDiscovery.DiscoverAsync(options.PathSpecs, cancellationToken);
        var results = executor.ExecuteAsync(pipeline, files, cancellationToken);

        var renderer = output.CreateRenderer<ListResult>(r => r.File);

        await renderer.RenderAsync(results, Console.Out, cancellationToken);
        return 0;
    }
}
