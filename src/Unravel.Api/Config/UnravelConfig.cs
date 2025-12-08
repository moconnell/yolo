using YoloAbstractions;
using YoloAbstractions.Config;

namespace Unravel.Api.Config;

public record UnravelConfig : ApiConfig
{
    public string DateFormat { get; init; } = "yyyy-MM-dd";

    public FactorType[] Factors { get; init; } = [];

    public UnravelExchange Exchange { get; init; } = UnravelExchange.Hyperliquid;

    public int Lookback { get; init; } = 360;

    public int UniverseSize { get; init; } = 20;

    public string UrlPathFactorsLive { get; init; } = "portfolio/factors-live?id={0}&tickers={1}";

    public string UrlPathFactors { get; init; } = "portfolio/factors?id={0}&tickers={1}&start_date={2}";

    public string UrlPathUniverse { get; init; } = "portfolio/universe?size={0}&exchange={1}&start_date={2}";
}