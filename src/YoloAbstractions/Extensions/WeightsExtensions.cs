
using System;
using YoloAbstractions.Config;

namespace YoloAbstractions.Extensions;

public static class WeightsExtensions
{
    public static decimal GetComboWeight(
        this Weight weight,
        YoloConfig config) => Math.Clamp(
            ((weight.CarryFactor * config.WeightingCarryFactor) +
            (weight.MomentumFactor * config.WeightingMomentumFactor) +
            (weight.TrendFactor * config.WeightingTrendFactor)) /
            (config.GetComboWeightDivisor() * (weight.Volatility > 0 ? weight.Volatility : 1)),
            -config.MaxWeightingAbs,
            config.MaxWeightingAbs);

    private static int GetComboWeightDivisor(this YoloConfig config) =>
        (config.WeightingCarryFactor > 0 ? 1 : 0) +
        (config.WeightingMomentumFactor > 0 ? 1 : 0) +
        (config.WeightingTrendFactor > 0 ? 1 : 0);
}