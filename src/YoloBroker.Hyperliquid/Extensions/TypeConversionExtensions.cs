using System;
using HyperLiquid.Net.Enums;

namespace YoloBroker.Hyperliquid.Extensions;

public static class TypeConversionExtensions
{
    public static YoloAbstractions.OrderSide ToYolo(this OrderSide orderSide) => orderSide switch
    {
        OrderSide.Buy => YoloAbstractions.OrderSide.Buy,
        OrderSide.Sell => YoloAbstractions.OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(orderSide), orderSide, null)
    };
    
    public static YoloAbstractions.OrderStatus ToYolo(this OrderStatus orderStatus) => orderStatus switch
    {
        OrderStatus.Filled => YoloAbstractions.OrderStatus.Filled,
        OrderStatus.Open => YoloAbstractions.OrderStatus.Open,
        OrderStatus.Canceled => YoloAbstractions.OrderStatus.Canceled,
        OrderStatus.Triggered => YoloAbstractions.OrderStatus.Triggered,
        OrderStatus.Rejected => YoloAbstractions.OrderStatus.Rejected,
        OrderStatus.MarginCanceled => YoloAbstractions.OrderStatus.MarginCanceled,
        OrderStatus.WaitingFill => YoloAbstractions.OrderStatus.WaitingFill,
        OrderStatus.WaitingTrigger => YoloAbstractions.OrderStatus.WaitingTrigger,
        _ => throw new ArgumentOutOfRangeException(nameof(orderStatus), orderStatus, null)
    };
}