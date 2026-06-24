using RobotWealth.Api.Data;
using YoloAbstractions;

namespace RobotWealth.Api;

public static class RobotWealthFactorTypeMapper
{
    private static readonly IReadOnlyDictionary<RobotWealthFactorType, FactorType> Map =
        new Dictionary<RobotWealthFactorType, FactorType>
        {
            [RobotWealthFactorType.CarryMegafactor] = FactorType.Carry,
            [RobotWealthFactorType.MomentumMegafactor] = FactorType.Momentum,
            [RobotWealthFactorType.TrendMegafactor] = FactorType.Trend,
            [RobotWealthFactorType.ExponentiallyWeightedVolatility] = FactorType.Volatility
        };

    public static IReadOnlyList<RobotWealthFactorType> All { get; } =
    [
        RobotWealthFactorType.CarryMegafactor,
        RobotWealthFactorType.MomentumMegafactor,
        RobotWealthFactorType.TrendMegafactor,
        RobotWealthFactorType.ExponentiallyWeightedVolatility
    ];

    public static FactorType ToFactorType(this RobotWealthFactorType factorType) =>
        Map.TryGetValue(factorType, out var commonFactorType)
            ? commonFactorType
            : throw new InvalidOperationException($"RobotWealth factor type {factorType} is not supported.");

    public static double GetValue(
        this RobotWealthFactorType factorType,
        RwWeight weight,
        RwVolatility volatility) =>
        factorType switch
        {
            RobotWealthFactorType.CarryMegafactor => weight.CarryMegafactor,
            RobotWealthFactorType.MomentumMegafactor => weight.MomentumMegafactor,
            RobotWealthFactorType.TrendMegafactor => weight.TrendMegafactor,
            RobotWealthFactorType.ExponentiallyWeightedVolatility => volatility.EwVol,
            _ => throw new InvalidOperationException($"RobotWealth factor type {factorType} is not supported.")
        };
}
