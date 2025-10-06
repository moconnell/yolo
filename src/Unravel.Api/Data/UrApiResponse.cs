using System.Text.Json.Serialization;
using YoloAbstractions.Interfaces;

namespace Unravel.Api.Data;

public class UrApiResponse<T> : IApiResponse<T>
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<T> Data { get; init; }

    [JsonPropertyName("index")]
    public required IReadOnlyList<string> Index { get; init; }

    [JsonPropertyName("columns")]
    public required IReadOnlyList<string> Columns { get; init; }

    [JsonPropertyName("success")]
    public required bool Success { get; init; }
}