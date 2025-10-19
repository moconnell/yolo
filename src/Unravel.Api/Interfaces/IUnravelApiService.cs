using YoloAbstractions;

namespace Unravel.Api.Interfaces;

public interface IUnravelApiService 
{
    IAsyncEnumerable<KeyValuePair<FactorType, FactorFrame>> GetFactorsAsync(
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default);

    Task<FactorDataFrame> GetFactorsLiveAsync(
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUniverseAsync(CancellationToken cancellationToken = default);
}