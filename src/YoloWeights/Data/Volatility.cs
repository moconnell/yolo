using System.Text.Json.Serialization;

namespace YoloWeights.Data;

public record Volatility
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("ewvol")]
    public double EwVol { get; init; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }
}