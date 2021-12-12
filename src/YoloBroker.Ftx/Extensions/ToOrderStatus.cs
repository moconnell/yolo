using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions;

public static partial class FtxExtensions
{
    public static OrderStatus ToOrderStatus(this FTX.Net.Enums.OrderStatus orderStatus)
    {
        return orderStatus switch
        {
            FTX.Net.Enums.OrderStatus.New => OrderStatus.New,
            FTX.Net.Enums.OrderStatus.Open => OrderStatus.Open,
            FTX.Net.Enums.OrderStatus.Closed => OrderStatus.Closed,
            _ => throw new ArgumentOutOfRangeException(nameof(orderStatus),
                orderStatus,
                "Order status not recognised")
        };
    }
}