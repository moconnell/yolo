using System;
using HyperLiquid.Net.Enums;

namespace YoloBroker.Hyperliquid.Extensions;

public static class TypeConversionExtensions
{
    public static YoloAbstractions.AssetType ToYolo(this SymbolType symbolType) => symbolType switch
    {
        SymbolType.Spot => YoloAbstractions.AssetType.Spot,
        SymbolType.Futures => YoloAbstractions.AssetType.Future,
        _ => throw new ArgumentOutOfRangeException(nameof(symbolType), symbolType, null)
    };

    public static YoloAbstractions.OrderSide ToYolo(this OrderSide orderSide) => orderSide switch
    {
        OrderSide.Buy => YoloAbstractions.OrderSide.Buy,
        OrderSide.Sell => YoloAbstractions.OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(orderSide), orderSide, null)
    };

    public static YoloAbstractions.OrderStatus ToYoloOrderStatus(this OrderStatus orderStatus) => orderStatus switch
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

    public static YoloAbstractions.OrderUpdateType ToYoloOrderUpdateType(this OrderStatus orderStatus) => orderStatus switch
    {
        OrderStatus.Filled => YoloAbstractions.OrderUpdateType.Filled,
        OrderStatus.Open => YoloAbstractions.OrderUpdateType.Created,
        OrderStatus.Canceled => YoloAbstractions.OrderUpdateType.Cancelled,
        OrderStatus.Triggered => YoloAbstractions.OrderUpdateType.Created,
        OrderStatus.Rejected => YoloAbstractions.OrderUpdateType.Error,
        OrderStatus.MarginCanceled => YoloAbstractions.OrderUpdateType.Cancelled,
        OrderStatus.WaitingFill => YoloAbstractions.OrderUpdateType.PartiallyFilled,
        OrderStatus.WaitingTrigger => YoloAbstractions.OrderUpdateType.Created,
        _ => throw new ArgumentOutOfRangeException(nameof(orderStatus), orderStatus, null)
    };

    internal static OrderSide ToHyperLiquid(this YoloAbstractions.OrderSide orderSide) => orderSide switch
    {
        YoloAbstractions.OrderSide.Buy => OrderSide.Buy,
        YoloAbstractions.OrderSide.Sell => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(orderSide), orderSide, null)
    };

    internal static OrderType ToHyperLiquid(this YoloAbstractions.OrderType orderType) => orderType switch
    {
        YoloAbstractions.OrderType.Market => OrderType.Market,
        YoloAbstractions.OrderType.Limit => OrderType.Limit,
        _ => throw new ArgumentOutOfRangeException(nameof(orderType), orderType, null)
    };
}