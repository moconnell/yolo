using System;
using System.Text.Json.Serialization;

namespace RobotWealth.Api.Data;

public record RwVolatility
{
    [JsonPropertyName("date")]
    public required DateTime Date { get; init; }

    [JsonPropertyName("ewvol")]
    public decimal EwVol { get; init; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }
}