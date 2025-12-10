namespace YoloAbstractions.Interfaces;

public interface ITradeFactory
{
    IEnumerable<Trade> CalculateTrades(
        IReadOnlyDictionary<string, decimal> weights,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets);
}