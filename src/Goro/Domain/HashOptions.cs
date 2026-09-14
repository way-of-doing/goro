using Goro.Hashing;
using Goro.Output;

namespace Goro.Domain;

public sealed record HashOptions(IReadOnlyList<string> PathSpecs, HashAlgorithmKind Algorithm, OutputFormat Output);
