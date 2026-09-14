using System.Text.Json.Serialization;

namespace Goro.Domain;

public sealed record ListResult([property: JsonPropertyName("file")] string File);
