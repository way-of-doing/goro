using Goro.Output;

namespace Goro.Domain;

public sealed record ListOptions(IReadOnlyList<string> PathSpecs, OutputFormat Output);
