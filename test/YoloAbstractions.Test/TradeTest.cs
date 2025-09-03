using System;
using Xunit;

namespace YoloAbstractions.Test;

public class TradeTest
{
    [Theory]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, OrderType.Limit, true, null,
        "BTC-PERP", AssetType.Future, -2, 45789, OrderType.Limit, true, null)]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, OrderType.Limit, true, null,
        "BTC/USD", AssetType.Spot, -2, 45789, OrderType.Limit, true, null,
        true)]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, OrderType.Limit, true, null,
        "BTC-PERP", AssetType.Future, -2, 45987, OrderType.Limit, true, null,
        true)]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, OrderType.Limit, true, null,
        "BTC-PERP", AssetType.Future, -2, 45789, OrderType.Limit, true, 1640179478L,
        true)]
    public void ShouldAddTrades(
        string assetName1, AssetType assetType1, decimal amount1, decimal limitPrice1, OrderType orderType1, bool? postPrice1, long? expiry1,
        string assetName2, AssetType assetType2, decimal amount2, decimal limitPrice2, OrderType orderType2, bool? postPrice2, long? expiry2,
        bool shouldThrow = false)
    {
        var trade1 = new Trade(assetName1, assetType1, amount1, limitPrice1, orderType1, postPrice1, ToDateTime(expiry1));
        var trade2 = new Trade(assetName2, assetType2, amount2, limitPrice2, orderType2, postPrice2, ToDateTime(expiry2));

        if (shouldThrow)
        {
            Assert.Throws<ArgumentException>(() => trade1 + trade2);
        }
        else
        {
            var trade3 = trade1 + trade2;
            Assert.Equal(amount1 + amount2, trade3.Amount);
        }
    }

    [Theory]
    [InlineData("BTC", 10, 4.5789, 10.0, true)]
    [InlineData("BTC", 10, 4.5789, null, true)]
    [InlineData("BTC", 1, 4.5789, 10.0, false)]
    [InlineData("BTC", 1, null, 10.0, true)]
#pragma warning disable xUnit1012 // Null should only be used for nullable parameters
    [InlineData(null, 1, null, 10.0, false)]
    public void ShouldCheckIfTradeIsTradable(string symbol, decimal amount, double? limitPrice, double? minOrderValue, bool expectedResult)
    {
        var (orderType, limitPriceDecimal) = ToOrderTypeAndDecimal(limitPrice);
        var trade = new Trade(symbol, AssetType.Future, amount, limitPriceDecimal, orderType);
        var result = trade.IsTradable(minOrderValue.HasValue ? Convert.ToDecimal(minOrderValue) : null);
        Assert.Equal(expectedResult, result);
    }

    private static (OrderType, decimal?) ToOrderTypeAndDecimal(double? limitPrice)
    {
        return limitPrice.HasValue ? (OrderType.Limit, Convert.ToDecimal(limitPrice)) : (OrderType.Market, null);
    }

    private static DateTime? ToDateTime(long? unixSeconds) =>
        unixSeconds.HasValue ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime : null;
}