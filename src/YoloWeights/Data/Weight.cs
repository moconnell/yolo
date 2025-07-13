using System.Text.Json.Serialization;

namespace YoloWeights.Data;

public class Weight
{
    [JsonPropertyName("arrival_price")]
    public double ArrivalPrice { get; set; }

    [JsonPropertyName("combo_weight")]
    public double ComboWeight { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("momentum_megafactor")]
    public double MomentumMegafactor { get; set; }

    [JsonPropertyName("ticker")]
    public string Ticker { get; set; }

    [JsonPropertyName("trend_megafactor")]
    public double TrendMegafactor { get; set; }
}