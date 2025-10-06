using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;

namespace YoloWeights;

public class YoloWeightsService : ICalcWeights
{
    private readonly IReadOnlyDictionary<FactorType, decimal> _factorWeights;
    private readonly decimal _maxWeightingAbs;
    private readonly IReadOnlyList<IGetFactors> _inner;

    public YoloWeightsService(IEnumerable<IGetFactors> inner, YoloConfig config)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(config);

        _inner = inner.ToArray();
        if (_inner.Count == 0)
        {
            throw new ArgumentException($"{nameof(inner)}: must have at least one factor provider");
        }

        _factorWeights = config.FactorWeights;
        _maxWeightingAbs = config.MaxWeightingAbs;
    }

    public async Task<IReadOnlyDictionary<string, Weight>> CalculateWeightsAsync(
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default)
    {
        var factors = await GetFactorsAsync(tickers, cancellationToken);

        var weights = new Dictionary<string, Weight>();

        foreach (var (ticker, factorDict) in factors)
        {
            var timeStamp = DateTime.UtcNow;

            var volatilityFactor = factorDict.TryGetValue(FactorType.Volatility, out var fac) && fac.Value > 0
                ? fac.Value
                : 1;

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

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<FactorType, Factor>>> GetFactorsAsync(
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default)
    {
        var baseAssets = new HashSet<string>(tickers);
        var result = new Dictionary<string, Dictionary<FactorType, Factor>>();

        foreach (var svc in _inner
                     .OrderBy(x => x.RequireTickers))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dict = await svc.GetFactorsAsync(baseAssets, cancellationToken);

            foreach (var (ticker, factors) in dict)
            {
                var (baseAsset, _) = ticker.GetBaseAndQuoteAssets();
                baseAssets.Add(baseAsset);

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

        return result.ToDictionary(
            kvp => kvp.Key,
            IReadOnlyDictionary<FactorType, Factor> (kvp) => kvp.Value);
    }
}