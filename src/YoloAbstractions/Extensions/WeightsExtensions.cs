using System;
using System.Collections.Generic;
using YoloAbstractions.Config;

namespace YoloAbstractions.Extensions;

public static class WeightsExtensions
{
    public static decimal GetComboWeight(
        this IReadOnlyDictionary<FactorType, Factor> factors,
        YoloConfig config)
    {
        ArgumentNullException.ThrowIfNull(factors);
        ArgumentNullException.ThrowIfNull(config);

        var weightings = config.FactorWeights;
        var totalWeight = 0m;
        var divisor = 0;

        foreach (var kvp in weightings)
        {
            if (kvp.Value <= 0)
                continue;
            
            totalWeight += factors[kvp.Key].Value * kvp.Value;
            divisor++;
        }

        var volatilityFactor = factors.TryGetValue(FactorType.Volatility, out var f) ? f.Value : 1;

        var v = Math.Clamp(
            totalWeight / (divisor * volatilityFactor),
            -config.MaxWeightingAbs,
            config.MaxWeightingAbs);

        return v;
    }
}