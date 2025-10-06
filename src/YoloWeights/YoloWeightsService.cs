using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Interfaces;

namespace YoloWeights;

public class YoloWeightsService(IEnumerable<IGetFactors> inner, YoloConfig config) : ICalcWeights
{
    private readonly IReadOnlyDictionary<FactorType, decimal> _factorWeights = config.FactorWeights;
    private readonly decimal _maxWeightingAbs = config.MaxWeightingAbs;

    public async Task<IReadOnlyDictionary<string, Weight>> CalculateWeightsAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default)
    {
        var factors = await GetFactorsAsync(tickers, cancellationToken);

        var weights = new Dictionary<string, Weight>();

        foreach (var (ticker, factorDict) in factors)
        {
            var timeStamp = DateTime.UtcNow;

            var volatilityFactor = factorDict.TryGetValue(FactorType.Volatility, out var fac) ? fac.Value : 1;

            var weightNumerator = 0m;
            var weightDenominator = 0;

            foreach (var (factorType, factor) in factorDict)
            {
                if (!_factorWeights.TryGetValue(factorType, out var factorWeight) || factorWeight == 0)
                {
                    continue;
                }

                weightNumerator += factor.Value * factorWeight;
                weightDenominator++;

                if (factor.TimeStamp < timeStamp)
                {
                    timeStamp = factor.TimeStamp;
                }
            }

            if (weightDenominator <= 0)
                continue;
            
            var rawWeightValue = weightNumerator / weightDenominator;

            var volAdjustedWeight = Math.Clamp(
                rawWeightValue / volatilityFactor,
                -_maxWeightingAbs,
                _maxWeightingAbs);

            weights[ticker] = new Weight(ticker, volAdjustedWeight, timeStamp);
        }

        return weights;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<FactorType, Factor>>> GetFactorsAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default)
    {
        var tasks = inner.Select(s => s.GetFactorsAsync(tickers, cancellationToken));

        return await Task.WhenAll(tasks).ContinueWith(t =>
        {
            var result = new Dictionary<string, Dictionary<FactorType, Factor>>();

            foreach (var dict in t.Result)
            {
                foreach (var (ticker, factors) in dict)
                {
                    if (!result.TryGetValue(ticker, out var factorsByType))
                    {
                        factorsByType = [];
                        result[ticker] = factorsByType;
                    }

                    foreach (var factorKvp in factors)
                    {
                        factorsByType[factorKvp.Key] = factorKvp.Value;
                    }
                }
            }

            return result.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyDictionary<FactorType, Factor>)kvp.Value);
        }, cancellationToken);
    }
}
