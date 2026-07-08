namespace YoloAbstractions;

public sealed record WeightsCalculationResult(
    IReadOnlyDictionary<string, decimal> Weights,
    FactorDataFrame RawFactors,
    FactorDataFrame NormalizedFactors)
{
    public static WeightsCalculationResult FromWeights(IReadOnlyDictionary<string, decimal> weights) =>
        new(weights, FactorDataFrame.Empty, FactorDataFrame.Empty);
}
