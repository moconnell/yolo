using YoloAbstractions;

namespace YoloBroker.Interface;

public interface IYoloBroker : IDisposable
{
    BrokerAccountContext GetAccountContext();

    Task CancelOrderAsync(Order order, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string? quoteCurrency = null,
        AssetPermissions assetPermissions = AssetPermissions.All,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<long, Order>> GetOpenOrdersAsync(CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, IReadOnlyList<Position>>> GetPositionsAsync(CancellationToken ct = default);

    Task<TradeResult> PlaceTradeAsync(Trade trade, CancellationToken ct = default);

    IAsyncEnumerable<TradeResult> PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken ct = default);

    IAsyncEnumerable<BrokerOrderEvent> SubscribeOrderUpdatesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<decimal>> GetDailyClosePricesAsync(
        string ticker,
        int periods,
        bool includeToday = false,
        CancellationToken ct = default);
}