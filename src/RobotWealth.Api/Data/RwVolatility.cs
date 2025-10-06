using System.Text.Json.Serialization;

namespace RobotWealth.Api.Data;

public record RwVolatility
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("ewvol")]
    public double EwVol { get; init; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }
}