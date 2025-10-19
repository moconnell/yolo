using System.Text.Json.Serialization;
using YoloAbstractions.Interfaces;

namespace Unravel.Api.Data;

public class UniverseResponse : IApiResponse<byte?[]>
{   
    [JsonPropertyName("data")]  
    public required IReadOnlyList<byte?[]> Data { get; init; }

    [JsonPropertyName("index")]
    public required IReadOnlyList<DateTime> Index { get; init; }

    [JsonPropertyName("columns")]
    public required IReadOnlyList<string> Tickers { get; init; }
}