using System;
using Xunit;

namespace YoloAbstractions.Test;

public class TradeTest
{
    [Theory]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, true, null,
        "BTC-PERP", AssetType.Future, -2, 45789, true, null)]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, true, null,
        "BTC/USD", AssetType.Spot, -2, 45789, true, null,
        true)]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, true, null,
        "BTC-PERP", AssetType.Future, -2, 45987, true, null,
        true)]
    [InlineData(
        "BTC-PERP", AssetType.Future, 10, 45789, true, null,
        "BTC-PERP", AssetType.Future, -2, 45789, true, 1640179478L,
        true)]
    public void ShouldAddTrades(
        string assetName1, AssetType assetType1, decimal amount1, decimal limitPrice1, bool? postPrice1, long? expiry1,
        string assetName2, AssetType assetType2, decimal amount2, decimal limitPrice2, bool? postPrice2, long? expiry2,
        bool shouldThrow = false)
    {
        var trade1 = new Trade(assetName1, assetType1, amount1, limitPrice1, postPrice1, ToDateTime(expiry1));
        var trade2 = new Trade(assetName2, assetType2, amount2, limitPrice2, postPrice2, ToDateTime(expiry2));

        if (shouldThrow)
        {
            Assert.Throws<ArgumentException>(() => trade1 + trade2);
        }
        else
        {
            var trade3 = trade1 + trade2;
            Assert.Equal(trade3.Amount, amount1 + amount2);
        }
    }

    private static DateTime? ToDateTime(long? ticks) => ticks.HasValue ? new DateTime(ticks.Value) : null;
}