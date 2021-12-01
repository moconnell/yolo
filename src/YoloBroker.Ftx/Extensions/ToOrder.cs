using FTX.Net.Objects;
using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions
{
    public static partial class FtxExtensions
    {
        public static Order? ToOrder(this FTXOrder? order)
        {
            return order is { }
                ? new Order(
                    order.Id,
                    order.Symbol,
                    order.CreatedAt,
                    order.Status.ToOrderStatus(),
                    order.Quantity,
                    order.RemainingQuantity,
                    order.OrderPrice,
                    order.ClientId
                )
                : null;
        }
    }
}