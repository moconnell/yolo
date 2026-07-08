namespace YoloAbstractions.Interfaces;

public interface ICalcWeights
{
    Task<WeightsCalculationResult> CalculateWeightsAsync(
        CancellationToken cancellationToken = default);
}
