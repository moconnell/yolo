using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YoloAbstractions.Interfaces;

public interface IGetFactors
{
    bool IsFixedUniverse { get; }

    Task<FactorDataFrame> GetFactorsAsync(
        IEnumerable<string>? tickers = null,
        CancellationToken cancellationToken = default);
}