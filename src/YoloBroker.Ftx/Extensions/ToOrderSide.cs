using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions;

public static partial class FtxExtensions
{
    private static OrderSide ToOrderSide(this FTX.Net.Enums.OrderSide orderSide)
    {
        return orderSide switch
        {
            FTX.Net.Enums.OrderSide.Buy => OrderSide.Buy,
            FTX.Net.Enums.OrderSide.Sell => OrderSide.Sell,
            _ => throw new ArgumentOutOfRangeException(nameof(orderSide),
                orderSide,
                "OrderSide not recognised")
        };
    }
}