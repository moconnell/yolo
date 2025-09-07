using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YoloWeights.Data;

public record ApiResponse<T>
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<T> Data { get; init; }

    [JsonPropertyName("last_updated")]
    public required long LastUpdated { get; init; }

    [JsonPropertyName("success")]
    public required string Success { get; init; }
}