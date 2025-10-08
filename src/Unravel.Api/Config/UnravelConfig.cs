using YoloAbstractions.Config;

namespace Unravel.Api.Config;

public record UnravelConfig : ApiConfig
{
    public FactorConfig[] Factors { get; init; } = [];
    public string FactorsUrlPath { get; init; } = "portfolio/factors-live?id={0}&tickers={1}";
}