using Microsoft.Extensions.Logging;
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
    private readonly NormalizationMethod _normalizationMethod;
    private readonly ILogger<YoloWeightsService> _logger;

    public YoloWeightsService(IEnumerable<IGetFactors> inner, YoloConfig config, ILogger<YoloWeightsService> logger)
    {
        _logger = logger;
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
        _normalizationMethod = config.FactorNormalizationMethod;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> CalculateWeightsAsync(
        CancellationToken cancellationToken = default)
    {
        var factorDataFrame = await GetFactorsAsync(cancellationToken);
        _logger.LogInformation("Factors (raw):\n{Factors}", factorDataFrame);

        var normalizedFactors = factorDataFrame.Normalize(_normalizationMethod);
        _logger.LogInformation("Factors (normalised):\n{Factors}", normalizedFactors);

        var weights = normalizedFactors.ApplyWeights(_factorWeights, _maxWeightingAbs);
        _logger.LogInformation("weights:\n{Weights}", weights);

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
            if (df.IsEmpty)
                continue;
            
            result.Add(df);
            factors.UnionWith(df.FactorTypes);

            foreach (var ticker in df.Tickers)
            {
                var (baseAsset, _) = ticker.GetBaseAndQuoteAssets();
                baseAssets.Add(baseAsset);
            }
        }

        return result.Count == 0 ? FactorDataFrame.Empty : result.Aggregate((one, two) => one + two);
    }
}