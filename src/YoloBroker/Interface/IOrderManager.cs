using YoloAbstractions;
using YoloAbstractions.Interfaces;

namespace YoloBroker.Interface;

public interface IOrderManager
{
    IAsyncEnumerable<OrderUpdate> ManageOrdersAsync(
        IEnumerable<Trade> trades,
        OrderManagementSettings settings,
        ITradeAdvisor advisor,
        CancellationToken ct = default);
}