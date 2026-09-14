using System.Text.Json.Serialization;

namespace Goro.Domain;

public sealed record HashResult(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("algo")] string Algo,
    [property: JsonPropertyName("hash")] string Hash);
