using YoloAbstractions;
using YoloAbstractions.Config;

namespace Unravel.Api.Config;

public record UnravelConfig : ApiConfig
{
    public string DateFormat { get; init; } = "yyyy-MM-dd";

    public FactorType[] Factors { get; init; } = [];

    public bool UseHistoricalFactors { get; init; }

    public UnravelExchange Exchange { get; init; } = UnravelExchange.Hyperliquid;

    public int Smoothing { get; init; }

    public int UniverseSize { get; init; } = 20;

    public string UrlPathFactorsLive { get; init; } = "portfolio/factors-live?id={0}&tickers={1}&smoothing={2}";

    public string UrlPathFactors { get; init; } = "portfolio/factors?id={0}&tickers={1}&smoothing={2}&start_date={3}";

    public string UrlPathUniverse { get; init; } = "portfolio/universe?size={0}&exchange={1}&start_date={2}";
}