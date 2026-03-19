using YoloAbstractions;

namespace YoloBroker.Interface;

public interface IOrderManager
{
    IAsyncEnumerable<OrderUpdate> ManageOrdersAsync(
        IEnumerable<Trade> trades,
        OrderManagementSettings settings,
        CancellationToken ct = default);
}