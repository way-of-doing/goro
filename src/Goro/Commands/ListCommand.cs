using Goro.Cli;
using Goro.Discovery;
using Goro.Domain;
using Goro.Execution;
using Goro.Output;
using Goro.Pipeline;
using Goro.Predicates.Binding;
using Goro.Predicates.Identifiers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Goro.Commands;

public sealed class ListCommand(
    IFileDiscoveryService fileDiscovery,
    IPipelinePlanner pipelinePlanner,
    IExecutor executor,
    IIdentifierCatalog identifiers)
    : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "[pathspecs]")]
        public string[] PathSpecs { get; set; } = [];

        [CommandOption("-o|--output <FORMAT>")]
        public string? Output { get; set; }

        [CommandOption("--filter <PREDICATE>")]
        public string? Filter { get; set; }

        public override ValidationResult Validate()
        {
            if (!OutputFormatExtensions.TryParse(Output, out _))
            {
                return ValidationResult.Error($"Invalid output format '{Output}'. Valid values: {string.Join(", ", OutputFormatExtensions.ValidNames)}.");
            }

            return base.Validate();
        }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        OutputFormatExtensions.TryParse(settings.Output, out var output);

        // The predicate is read before the pathspecs are resolved: it needs nothing but its own
        // text, so a predicate with errors in it is rejected without the file system being touched,
        // and the run never starts. See docs/concepts/predicates.md.
        CompiledPredicate? filter = null;
        if (settings.Filter is { } text)
        {
            var compiled = PredicateCompiler.Compile(text, identifiers);
            if (!compiled.Succeeded)
            {
                PredicateDiagnosticRenderer.Write(Console.Error, text, compiled.Diagnostics);
                return Task.FromResult(ExitCodes.Rejected);
            }

            filter = compiled.Value;
        }

        var pathSpecs = OptionParsing.ResolvePathSpecs(settings.PathSpecs, context.Remaining.Raw);
        var options = new ListOptions(pathSpecs, output, filter);

        var renderer = output.CreateRenderer<ListResult>(r => r.File);

        return new FileRun(fileDiscovery, executor)
            .ExecuteAsync(settings, options.PathSpecs, () => pipelinePlanner.PlanList(options), renderer, cancellationToken);
    }
}
