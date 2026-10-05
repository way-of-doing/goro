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
    /// <summary>What a file whose audio could not be read shows in place of its hash, in plain output.</summary>
    public const string AbsentHash = "-";

    public sealed class Settings : GlobalSettings
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
                return ValidationResult.Error(ErrorMessages.InvalidAlgorithm(Algorithm, HashAlgorithmKindExtensions.ValidNames));
            }

            if (!OutputFormatExtensions.TryParse(Output, out _))
            {
                return ValidationResult.Error(ErrorMessages.InvalidOutputFormat(Output, OutputFormatExtensions.ValidNames));
            }

            return base.Validate();
        }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        HashAlgorithmKindExtensions.TryParse(settings.Algorithm, out var algorithm);
        OutputFormatExtensions.TryParse(settings.Output, out var output);

        var pathSpecs = OptionParsing.ResolvePathSpecs(settings.PathSpecs, context.Remaining.Raw);
        var options = new HashOptions(pathSpecs, algorithm, output);

        // A file whose audio could not be read still has its row, with the hash absent: "-" here,
        // and null in JSON. See docs/commands/hash.md.
        var renderer = output.CreateRenderer<HashResult>(r => $"{r.File} {r.Algo} {r.Hash ?? AbsentHash}");

        return new FileRun(fileDiscovery, executor)
            .ExecuteAsync(settings, options.PathSpecs, () => pipelinePlanner.PlanHash(options), renderer, cancellationToken);
    }
}
