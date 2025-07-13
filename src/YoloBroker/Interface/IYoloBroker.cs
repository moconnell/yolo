using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;

namespace YoloBroker.Interface;

public interface IYoloBroker : IDisposable
{
    Task<Dictionary<long, Order>> GetOrdersAsync(CancellationToken ct);
    
    Task<IDictionary<string, IEnumerable<Position>>> GetPositionsAsync(CancellationToken ct);

    IAsyncEnumerable<TradeResult> PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken ct);

    Task<IDictionary<string, IEnumerable<MarketInfo>>> GetMarketsAsync(
        ISet<string>? baseAssetFilter = null,
        string? quoteCurrency = null,
        AssetPermissions assetPermissions = AssetPermissions.All,
        CancellationToken ct = default);
}