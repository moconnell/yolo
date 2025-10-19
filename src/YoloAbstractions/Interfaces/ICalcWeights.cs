using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YoloAbstractions.Interfaces;

public interface ICalcWeights
{
    Task<IReadOnlyDictionary<string, decimal>> CalculateWeightsAsync(
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default);
}