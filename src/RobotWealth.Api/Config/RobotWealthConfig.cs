using YoloAbstractions.Config;

namespace RobotWealth.Api.Config;

public record RobotWealthConfig : ApiConfig
{
    public string VolatilitiesUrlPath { get; init; } = "volatilities";
    public string WeightsUrlPath { get; init; } = "weights";
}
