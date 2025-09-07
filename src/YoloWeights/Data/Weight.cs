using System.Text.Json.Serialization;

namespace YoloWeights.Data;

public record Weight
{
    [JsonPropertyName("arrival_price")]
    public double ArrivalPrice { get; init; }

    [JsonPropertyName("carry_megafactor")]
    public double CarryMegafactor { get; init; }

    [JsonPropertyName("combo_weight")]
    public double ComboWeight { get; init; }

    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("momentum_megafactor")]
    public double MomentumMegafactor { get; init; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }

    [JsonPropertyName("trend_megafactor")]
    public double TrendMegafactor { get; init; }
}