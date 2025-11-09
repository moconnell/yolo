using System.Security.Cryptography;
using YoloAbstractions;

namespace YoloBroker.Hyperliquid.Test;

internal static class TradeUtil
{
    internal static Trade CreateTrade(string symbol, AssetType assetType, double quantity, decimal? price) =>
        new(
            symbol,
            assetType,
            Convert.ToDecimal(quantity),
            price,
            ClientOrderId: $"0x{RandomNumberGenerator.GetHexString(32, true)}");
}