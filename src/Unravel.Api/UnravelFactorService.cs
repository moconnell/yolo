using Unravel.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Interfaces;

namespace Unravel.Api;

public class UnravelFactorService : IGetFactors
{
    private readonly IUnravelApiService _unravelApiService;

    public UnravelFactorService(IUnravelApiService unravelApiService) => _unravelApiService = unravelApiService;

    public bool IsFixedUniverse => false;

    public async Task<FactorDataFrame> GetFactorsAsync(
        IEnumerable<string>? tickers = null,
        CancellationToken cancellationToken = default)
    {
        var tickersArray = await GetTickers(tickers, cancellationToken);
        if (tickersArray.Count == 0)
        {
            throw new ArgumentException("No tickers provided or resolved", nameof(tickers));
        }

        var factorsLive = await _unravelApiService.GetFactorsLiveAsync(
            tickersArray,
            cancellationToken);

        return factorsLive;
    }

    private async Task<IReadOnlyList<string>> GetTickers(
        IEnumerable<string>? tickers,
        CancellationToken cancellationToken = default)
    {
        var tickerArray = tickers != null ? tickers as string[] ?? tickers.ToArray() : [];
        if (tickerArray.Length != 0)
        {
            return tickerArray;
        }

        return await _unravelApiService.GetUniverseAsync(cancellationToken);
    }
}