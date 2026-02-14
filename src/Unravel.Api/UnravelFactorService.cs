using Unravel.Api.Config;
using Unravel.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Interfaces;

namespace Unravel.Api;

public class UnravelFactorService : IGetFactors
{
    private readonly IUnravelApiService _unravelApiService;
    private readonly UnravelConfig _unravelConfig;

    public UnravelFactorService(IUnravelApiService unravelApiService, UnravelConfig unravelConfig)
    {
        _unravelApiService = unravelApiService;
        _unravelConfig = unravelConfig;
    }

    public bool IsFixedUniverse => false;

    public int Order => 10;

    public async Task<FactorDataFrame> GetFactorsAsync(
        IEnumerable<string>? tickers = null,
        ISet<FactorType>? factors = null,
        CancellationToken cancellationToken = default)
    {
        var tickersArray = await GetTickers(tickers, cancellationToken);
        if (tickersArray.Count == 0)
        {
            throw new ApiException("No tickers provided or resolved");
        }

        var factorsLive = _unravelConfig.UseLiveFactors
            ? await _unravelApiService.GetFactorsLiveAsync(
                tickersArray,
                cancellationToken: cancellationToken)
            : await _unravelApiService.GetFactorsHistoricalAsync(
                tickersArray,
                cancellationToken: cancellationToken);

        return factorsLive;
    }

    private async Task<IReadOnlyList<string>> GetTickers(
        IEnumerable<string>? tickers,
        CancellationToken cancellationToken = default)
    {
        var tickerArray = tickers?.Distinct().ToArray() ?? [];
        return tickerArray.Length > 0 ? tickerArray : await _unravelApiService.GetUniverseAsync(cancellationToken);
    }
}