using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;

namespace YoloBroker.Interface;

public interface IYoloBroker : IDisposable
{
    Task CancelOrderAsync(Order order, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string? quoteCurrency = null,
        AssetPermissions assetPermissions = AssetPermissions.All,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<long, Order>> GetOpenOrdersAsync(CancellationToken ct);

    Task<IReadOnlyDictionary<string, IReadOnlyList<Position>>> GetPositionsAsync(CancellationToken ct);

    IAsyncEnumerable<OrderUpdate> ManageOrdersAsync(
        IEnumerable<Trade> trades,
        OrderManagementSettings settings,
        CancellationToken ct = default);

    Task<TradeResult> PlaceTradeAsync(Trade trade, CancellationToken ct);

    IAsyncEnumerable<TradeResult> PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken ct);
    
    Task<IReadOnlyList<decimal>> GetDailyClosePricesAsync(string ticker, int periods, CancellationToken ct);
}