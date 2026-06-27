using System.Security.Cryptography;
using YoloAbstractions;

namespace YoloBroker.Hyperliquid.Test;

internal static class TradeUtil
{
    internal static Trade CreateTrade(string symbol, AssetType assetType, double quantity, decimal price, OrderType orderType = OrderType.Limit, bool postOnly = true) =>
        CreateTrade(symbol, assetType, Convert.ToDecimal(quantity), price, orderType, postOnly);

    internal static Trade CreateTrade(string symbol, AssetType assetType, decimal quantity, decimal price, OrderType orderType = OrderType.Limit, bool postOnly = true) =>
        new(
            symbol,
            assetType,
            quantity,
            price,
            orderType,
            postOnly,
            ClientOrderId: $"0x{RandomNumberGenerator.GetHexString(32, true)}");
}
