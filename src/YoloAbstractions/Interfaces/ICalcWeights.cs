namespace YoloAbstractions.Interfaces;

public interface ICalcWeights
{
    Task<IReadOnlyDictionary<string, decimal>> CalculateWeightsAsync(
        CancellationToken cancellationToken = default);
}