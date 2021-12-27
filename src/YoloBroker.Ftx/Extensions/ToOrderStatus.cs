using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions;

public static partial class FtxExtensions
{
    private static OrderStatus ToOrderStatus(this FTX.Net.Enums.OrderStatus orderStatus, decimal quantityRemaining)
    {
        return orderStatus switch
        {
            FTX.Net.Enums.OrderStatus.New => OrderStatus.New,
            FTX.Net.Enums.OrderStatus.Open => OrderStatus.Open,
            FTX.Net.Enums.OrderStatus.Closed when quantityRemaining > 0 => OrderStatus.Cancelled,
            FTX.Net.Enums.OrderStatus.Closed when quantityRemaining == 0 => OrderStatus.Filled,
            _ => throw new ArgumentOutOfRangeException(nameof(orderStatus),
                orderStatus,
                "Order status not recognised")
        };
    }
}