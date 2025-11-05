using System.Text.Json.Serialization;
using YoloAbstractions.Interfaces;

namespace Unravel.Api.Data;
    
public class FactorsResponse : IApiResponse<double[]>
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<double[]> Data { get; init; }

    [JsonPropertyName("index")]
    public required IReadOnlyList<DateTime> Index { get; init; }

    [JsonPropertyName("columns")]
    public required IReadOnlyList<string> Tickers { get; init; }
}