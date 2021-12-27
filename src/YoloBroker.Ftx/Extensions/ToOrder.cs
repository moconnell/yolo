using FTX.Net.Objects;
using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions;

public static partial class FtxExtensions
{
    public static Order? ToOrder(this FTXOrder? order) =>
        order is { }
            ? new Order(
                order.Id,
                order.Symbol,
                order.CreatedAt,
                order.Side.ToOrderSide(),
                order.Status.ToOrderStatus(order.RemainingQuantity),
                order.Quantity,
                order.RemainingQuantity,
                order.OrderPrice,
                order.ClientId
            )
            : null;
}