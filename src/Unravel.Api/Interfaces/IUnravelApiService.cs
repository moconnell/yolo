using YoloAbstractions;

namespace Unravel.Api.Interfaces;

public interface IUnravelApiService
{
    Task<FactorDataFrame> GetFactorsLiveAsync(
        IEnumerable<string> tickers,
        bool throwOnMissingValue = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUniverseAsync(CancellationToken cancellationToken = default);
}