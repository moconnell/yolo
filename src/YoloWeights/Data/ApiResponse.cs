using System.Collections.Generic;
using System.Text.Json.Serialization;
using YoloAbstractions.Interfaces;

namespace YoloWeights.Data;

public record ApiResponse<T> : IApiResponse<T>
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<T> Data { get; init; }

    [JsonPropertyName("last_updated")]
    public required long LastUpdated { get; init; }

    [JsonPropertyName("success")]
    public required bool Success { get; init; }
}