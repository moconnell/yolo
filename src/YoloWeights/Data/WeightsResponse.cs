using System.Text.Json.Serialization;

namespace YoloWeights.Data;

public class WeightsResponse
{
    [JsonPropertyName("data")]
    public Weight[] Data { get; set; }

    [JsonPropertyName("success")]
    public string Success { get; set; }
}