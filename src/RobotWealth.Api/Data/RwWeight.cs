using System;
using System.Text.Json.Serialization;

namespace RobotWealth.Api.Data;

public record RwWeight
{
    [JsonPropertyName("arrival_price")]
    public decimal ArrivalPrice { get; init; }

    [JsonPropertyName("carry_megafactor")]
    public decimal CarryMegafactor { get; init; }

    [JsonPropertyName("combo_weight")]
    public decimal ComboWeight { get; init; }

    [JsonPropertyName("date")]
    public required DateTime Date { get; init; }

    [JsonPropertyName("momentum_megafactor")]
    public decimal MomentumMegafactor { get; init; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }

    [JsonPropertyName("trend_megafactor")]
    public decimal TrendMegafactor { get; init; }
}