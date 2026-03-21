namespace YoloAbstractions.Interfaces;

public interface ITradeAdvisor
{
    /// <summary>
    /// Called when a limit order times out. Returns the replacement trade to place (with fresh
    /// prices and positions), or null if the position is already within target (nothing to do).
    /// </summary>
    Task<Trade?> GetReplacementTradeAsync(Trade timedOutTrade, CancellationToken ct = default);
}
