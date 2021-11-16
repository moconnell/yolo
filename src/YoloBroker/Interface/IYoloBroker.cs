using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YoloAbstractions;

namespace YoloBroker
{
    public interface IYoloBroker
    {
        Task<IDictionary<string, Position>> GetPositionsAsync(CancellationToken ct);

        Task<Dictionary<string, IEnumerable<Price>>> GetPricesAsync(CancellationToken ct);

        Task PlaceTradesAsync(IEnumerable<Trade> trades, CancellationToken ct);

        Task ConnectAsync(CancellationToken ct);

        Task<IDictionary<string, SymbolInfo>> GetSymbolsAsync(
            IEnumerable<string>? symbols = null,
            CancellationToken ct = default);
    }
}