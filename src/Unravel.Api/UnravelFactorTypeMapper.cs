using YoloAbstractions;

namespace Unravel.Api;

public static class UnravelFactorTypeMapper
{
    private static readonly IReadOnlyDictionary<UnravelFactorType, (string ApiName, FactorType FactorType)> Map =
        new Dictionary<UnravelFactorType, (string ApiName, FactorType FactorType)>
        {
            [UnravelFactorType.Altair] = ("altair", FactorType.Altair),
            [UnravelFactorType.Carry] = ("carry_enhanced", FactorType.Carry),
            [UnravelFactorType.EnhancedMeanReversion] = ("mean_reversion_enhanced", FactorType.EnhancedMeanReversion),
            [UnravelFactorType.EnhancedMomentum] = ("momentum_enhanced", FactorType.EnhancedMomentum),
            [UnravelFactorType.InstantaneousMomentum] = ("instantaneous_momentum", FactorType.InstantaneousMomentum),
            [UnravelFactorType.InstantaneousVolatility] = ("instantaneous_volatility", FactorType.InstantaneousVolatility),
            [UnravelFactorType.MarginRisk] = ("margin_risk", FactorType.MarginRisk),
            [UnravelFactorType.MeanReversion] = ("mean_reversion", FactorType.MeanReversion),
            [UnravelFactorType.Momentum] = ("momentum", FactorType.Momentum),
            [UnravelFactorType.OpenInterestDivergence] = ("open_interest_divergence", FactorType.OpenInterestDivergence),
            [UnravelFactorType.Polaris] = ("polaris", FactorType.Polaris),
            [UnravelFactorType.RelativeIlliquidity] = ("relative_illiquidity", FactorType.RelativeIlliquidity),
            [UnravelFactorType.RetailFlow] = ("retail_flow", FactorType.RetailFlow),
            [UnravelFactorType.SupplyVelocity] = ("supply_velocity", FactorType.SupplyVelocity),
            [UnravelFactorType.TrendLongonlyAdaptive] = ("trend_longonly_adaptive", FactorType.TrendLongonlyAdaptive)
        };

    public static string ToApiName(this UnravelFactorType factorType) =>
        TryGetMapping(factorType).ApiName;

    public static FactorType ToFactorType(this UnravelFactorType factorType) =>
        TryGetMapping(factorType).FactorType;

    private static (string ApiName, FactorType FactorType) TryGetMapping(UnravelFactorType factorType) =>
        Map.TryGetValue(factorType, out var mapping)
            ? mapping
            : throw new InvalidOperationException($"Unravel factor type {factorType} is not supported.");
}
