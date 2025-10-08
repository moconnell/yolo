using YoloAbstractions.Config;

namespace Unravel.Api.Config;

public record UnravelConfig : ApiConfig
{
    public FactorConfig[] Factors { get; init; } = [];

    public string FactorsLiveUrlPath { get; init; } = "portfolio/factors-live?id={0}&tickers={1}";

    public string FactorsUrlPath { get; init; } = "portfolio/factors?id={0}&tickers={1}&start_date={2}";

    public string NormalizedSeriesUrlPath { get; init; } = "normalized-series?series={0}&ticker={1}";
}