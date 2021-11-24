using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;

namespace YoloBroker
{
    public interface IYoloBroker : IDisposable
    {
        Task<IDictionary<string, Position>> GetPositionsAsync(CancellationToken ct);

        Task PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken ct);

        Task ConnectAsync(CancellationToken ct);

        Task<IDictionary<string, IEnumerable<MarketInfo>>> GetMarketsAsync(CancellationToken ct = default);
    }
}