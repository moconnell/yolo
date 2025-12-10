using HyperLiquid.Net.Enums;
using Shouldly;
using YoloBroker.Hyperliquid.Extensions;

namespace YoloBroker.Hyperliquid.Test.Extensions;

public class TypeConversionExtensionsTest
{
    [Theory]
    [InlineData(SymbolType.Spot, YoloAbstractions.AssetType.Spot)]
    [InlineData(SymbolType.Futures, YoloAbstractions.AssetType.Future)]
    public void ToYolo_SymbolType_ShouldConvertCorrectly(SymbolType symbolType, YoloAbstractions.AssetType expected)
    {
        var result = symbolType.ToYolo();
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToYolo_SymbolType_InvalidValue_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidSymbolType = (SymbolType)999;
        Should.Throw<ArgumentOutOfRangeException>(() => invalidSymbolType.ToYolo());
    }

    [Theory]
    [InlineData(OrderSide.Buy, YoloAbstractions.OrderSide.Buy)]
    [InlineData(OrderSide.Sell, YoloAbstractions.OrderSide.Sell)]
    public void ToYolo_OrderSide_ShouldConvertCorrectly(OrderSide orderSide, YoloAbstractions.OrderSide expected)
    {
        var result = orderSide.ToYolo();
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToYolo_OrderSide_InvalidValue_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidOrderSide = (OrderSide)999;
        Should.Throw<ArgumentOutOfRangeException>(() => invalidOrderSide.ToYolo());
    }

    [Theory]
    [InlineData(OrderStatus.Filled, YoloAbstractions.OrderStatus.Filled)]
    [InlineData(OrderStatus.Open, YoloAbstractions.OrderStatus.Open)]
    [InlineData(OrderStatus.Canceled, YoloAbstractions.OrderStatus.Canceled)]
    [InlineData(OrderStatus.Triggered, YoloAbstractions.OrderStatus.Triggered)]
    [InlineData(OrderStatus.Rejected, YoloAbstractions.OrderStatus.Rejected)]
    [InlineData(OrderStatus.MarginCanceled, YoloAbstractions.OrderStatus.MarginCanceled)]
    [InlineData(OrderStatus.WaitingFill, YoloAbstractions.OrderStatus.WaitingFill)]
    [InlineData(OrderStatus.WaitingTrigger, YoloAbstractions.OrderStatus.WaitingTrigger)]
    public void ToYoloOrderStatus_ShouldConvertCorrectly(OrderStatus orderStatus, YoloAbstractions.OrderStatus expected)
    {
        var result = orderStatus.ToYoloOrderStatus();
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToYoloOrderStatus_InvalidValue_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidOrderStatus = (OrderStatus)999;
        Should.Throw<ArgumentOutOfRangeException>(() => invalidOrderStatus.ToYoloOrderStatus());
    }

    [Theory]
    [InlineData(OrderStatus.Filled, YoloAbstractions.OrderUpdateType.Filled)]
    [InlineData(OrderStatus.Open, YoloAbstractions.OrderUpdateType.Created)]
    [InlineData(OrderStatus.Canceled, YoloAbstractions.OrderUpdateType.Cancelled)]
    [InlineData(OrderStatus.Triggered, YoloAbstractions.OrderUpdateType.Created)]
    [InlineData(OrderStatus.Rejected, YoloAbstractions.OrderUpdateType.Error)]
    [InlineData(OrderStatus.MarginCanceled, YoloAbstractions.OrderUpdateType.Cancelled)]
    [InlineData(OrderStatus.WaitingFill, YoloAbstractions.OrderUpdateType.PartiallyFilled)]
    [InlineData(OrderStatus.WaitingTrigger, YoloAbstractions.OrderUpdateType.Created)]
    public void ToYoloOrderUpdateType_ShouldConvertCorrectly(OrderStatus orderStatus, YoloAbstractions.OrderUpdateType expected)
    {
        var result = orderStatus.ToYoloOrderUpdateType();
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToYoloOrderUpdateType_InvalidValue_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidOrderStatus = (OrderStatus)999;
        Should.Throw<ArgumentOutOfRangeException>(() => invalidOrderStatus.ToYoloOrderUpdateType());
    }

    [Theory]
    [InlineData(YoloAbstractions.OrderSide.Buy, OrderSide.Buy)]
    [InlineData(YoloAbstractions.OrderSide.Sell, OrderSide.Sell)]
    public void ToHyperLiquid_OrderSide_ShouldConvertCorrectly(YoloAbstractions.OrderSide orderSide, OrderSide expected)
    {
        var result = orderSide.ToHyperLiquid();
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToHyperLiquid_OrderSide_InvalidValue_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidOrderSide = (YoloAbstractions.OrderSide)999;
        Should.Throw<ArgumentOutOfRangeException>(() => invalidOrderSide.ToHyperLiquid());
    }

    [Theory]
    [InlineData(YoloAbstractions.OrderType.Market, OrderType.Market)]
    [InlineData(YoloAbstractions.OrderType.Limit, OrderType.Limit)]
    public void ToHyperLiquid_OrderType_ShouldConvertCorrectly(YoloAbstractions.OrderType orderType, OrderType expected)
    {
        var result = orderType.ToHyperLiquid();
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToHyperLiquid_OrderType_InvalidValue_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidOrderType = (YoloAbstractions.OrderType)999;
        Should.Throw<ArgumentOutOfRangeException>(() => invalidOrderType.ToHyperLiquid());
    }
}