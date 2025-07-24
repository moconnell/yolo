using System.Text.Json.Serialization;

namespace YoloWeights.Data;

public class WeightsResponse
{
    [JsonPropertyName("data")]
    public required Weight[] Data { get; set; }

    [JsonPropertyName("success")]
    public required string Success { get; set; }
}