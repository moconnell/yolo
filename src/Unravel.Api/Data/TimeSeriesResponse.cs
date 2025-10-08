using System.Text.Json.Serialization;
using YoloAbstractions.Interfaces;

namespace Unravel.Api.Data;

public class TimeSeriesResponse : IApiResponse<double>
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<double> Data { get; init; }

    [JsonPropertyName("index")]
    public required IReadOnlyList<DateTime> Index { get; init; }
}