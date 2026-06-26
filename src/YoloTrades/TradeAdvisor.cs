using YoloAbstractions;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloBroker.Interface;

namespace YoloTrades;

/// <summary>
/// Recalculates the best trade for a timed-out order by fetching fresh prices and current
/// positions from the broker and re-running the trade factory with the fixed run weights.
/// Returns null if the position is already within target (nothing to do).
/// </summary>
public class TradeAdvisor : ITradeAdvisor
{
    private readonly IReadOnlyDictionary<string, decimal> _weights;
    private readonly ITradeFactory _tradeFactory;
    private readonly IYoloBroker _broker;
    private readonly string _baseAsset;
    private readonly AssetPermissions _assetPermissions;

    public TradeAdvisor(
        IReadOnlyDictionary<string, decimal> weights,
        ITradeFactory tradeFactory,
        IYoloBroker broker,
        string baseAsset,
        AssetPermissions assetPermissions)
    {
        _weights = weights;
        _tradeFactory = tradeFactory;
        _broker = broker;
        _baseAsset = baseAsset;
        _assetPermissions = assetPermissions;
    }

    public async Task<Trade?> GetReplacementTradeAsync(Trade timedOutTrade, CancellationToken ct = default)
    {
        var positions = await _broker.GetPositionsAsync(ct);
        var baseAssetFilter = positions.Keys
            .Union(_weights.Keys.Select(x => x.GetBaseAndQuoteAssets().BaseAsset))
            .Append(timedOutTrade.Symbol)
            .ToHashSet();

        var markets = await _broker.GetMarketsAsync(
            baseAssetFilter,
            _baseAsset,
            _assetPermissions,
            ct);

        return _tradeFactory
            .CalculateTrades(_weights, positions, markets)
            .FirstOrDefault(t => t.Symbol == timedOutTrade.Symbol);
    }
}
