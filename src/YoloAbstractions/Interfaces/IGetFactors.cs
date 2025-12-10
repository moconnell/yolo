namespace YoloAbstractions.Interfaces;

public interface IGetFactors
{
    bool IsFixedUniverse { get; }
    
    int Order { get; }

    Task<FactorDataFrame> GetFactorsAsync(
        IEnumerable<string>? tickers = null,
        ISet<FactorType>? factors = null,
        CancellationToken cancellationToken = default);
}