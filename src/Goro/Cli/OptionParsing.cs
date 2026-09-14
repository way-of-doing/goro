namespace Goro.Cli;

/// <summary>
/// Helpers for resolving the raw structures Spectre.Console.Cli hands back into what
/// a command actually needs. Parsing/validating individual option *values* (e.g.
/// algorithm or output format names) lives on the enums themselves
/// (<see cref="Goro.Hashing.HashAlgorithmKindExtensions"/>,
/// <see cref="Goro.Output.OutputFormatExtensions"/>) rather than here.
/// </summary>
public static class OptionParsing
{
    /// <summary>
    /// Combines the positionally-bound pathspecs with anything Spectre.Console.Cli
    /// routed to <see cref="Spectre.Console.Cli.CommandContext.Remaining"/> (tokens
    /// after a <c>--</c> separator aren't bound to <c>[CommandArgument]</c> properties
    /// automatically), defaulting to the current directory when both are empty.
    /// </summary>
    public static IReadOnlyList<string> ResolvePathSpecs(IEnumerable<string> pathSpecs, IEnumerable<string> remaining)
    {
        var combined = pathSpecs.Concat(remaining).ToArray();
        return combined.Length > 0 ? combined : ["."];
    }
}
