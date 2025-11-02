using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;

namespace YoloWeights;

public class YoloWeightsService : ICalcWeights
{
    private readonly IGetFactors[] _inner;
    private readonly IReadOnlyDictionary<FactorType, double> _factorWeights;
    private readonly double? _maxWeightingAbs;

    public YoloWeightsService(IEnumerable<IGetFactors> inner, YoloConfig config)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(config);

        _inner = inner.ToArray();
        if (_inner.Length == 0)
        {
            throw new ArgumentException($"{nameof(inner)}: must have at least one factor provider");
        }

        _factorWeights = new Dictionary<FactorType, double>(
            config.FactorWeights.Select(kvp =>
                new KeyValuePair<FactorType, double>(kvp.Key, Convert.ToDouble(kvp.Value))));
        _maxWeightingAbs = config.MaxWeightingAbs;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> CalculateWeightsAsync(
        CancellationToken cancellationToken = default)
    {
        var df = await GetFactorsAsync(cancellationToken);
        var weights = df.ApplyWeights(_factorWeights, _maxWeightingAbs);
        var weightsDict = weights.Rows.ToDictionary(
            r => (string) r["Ticker"],
            r => Convert.ToDecimal((double) r["Weight"]));

        return weightsDict;
    }

    private async Task<FactorDataFrame> GetFactorsAsync(
        CancellationToken cancellationToken = default)
    {
        var baseAssets = new HashSet<string>();
        var factors = new HashSet<FactorType>();
        var result = new List<FactorDataFrame>();

        foreach (var svc in _inner
                     .OrderByDescending(x => x.IsFixedUniverse)
                     .ThenBy(x => x.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var df = await svc.GetFactorsAsync(baseAssets, factors, cancellationToken);
            result.Add(df);
            factors.UnionWith(df.FactorTypes);

            foreach (var ticker in df.Tickers)
            {
                var (baseAsset, _) = ticker.GetBaseAndQuoteAssets();
                baseAssets.Add(baseAsset);
            }
        }

        return result.Aggregate((one, two) => one + two);
    }
}