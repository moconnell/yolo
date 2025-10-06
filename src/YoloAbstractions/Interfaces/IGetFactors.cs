using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YoloAbstractions.Interfaces;

public interface IGetFactors
{
    Task<IReadOnlyDictionary<string, Dictionary<FactorType, Factor>>> GetFactorsAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default);
}