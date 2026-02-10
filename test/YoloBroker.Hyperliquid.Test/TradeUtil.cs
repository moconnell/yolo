using System.Security.Cryptography;
using YoloAbstractions;

namespace YoloBroker.Hyperliquid.Test;

internal static class TradeUtil
{
    internal static Trade CreateTrade(string symbol, AssetType assetType, double quantity, decimal price, OrderType orderType = OrderType.Limit) =>
        new(
            symbol,
            assetType,
            Convert.ToDecimal(quantity),
            price,
            orderType,
            ClientOrderId: $"0x{RandomNumberGenerator.GetHexString(32, true)}");
}