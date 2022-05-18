using FTX.Net.Objects;
using FTX.Net.Objects.Models;
using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions;

public static partial class FtxExtensions
{
    public static Order? ToOrder(this FTXOrder? order) =>
        order is { }
            ? new Order(
                order.Id,
                order.Symbol,
                order.CreateTime,
                order.Side.ToOrderSide(),
                order.Status.ToOrderStatus(order.QuantityRemaining),
                order.Quantity,
                order.QuantityRemaining,
                order.Price,
                order.ClientOrderId
            )
            : null;
}