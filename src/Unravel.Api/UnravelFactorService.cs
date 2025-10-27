using Unravel.Api.Interfaces;
using YoloAbstractions;
using YoloAbstractions.Exceptions;
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
            throw new ApiException("No tickers provided or resolved");
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
        return await _unravelApiService.GetUniverseAsync(cancellationToken);
        // var tickerArray = tickers != null ? tickers.Select(x => x.ToUpperInvariant()).Distinct().ToArray() : [];
        // return tickerArray.Length != 0 ? tickerArray : await _unravelApiService.GetUniverseAsync(cancellationToken);
    }
}