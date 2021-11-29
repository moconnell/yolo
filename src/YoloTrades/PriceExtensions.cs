using System;
using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

public static class PriceExtensions
{
    public static IEnumerable<Price> GetPrices(
        this IDictionary<string, IEnumerable<Price>> prices,
        string symbol,
        string baseCurrencyToken,
        AssetType? assetType = null)
    {
        var cross = $"{symbol}{baseCurrencyToken}";
        return prices.GetPrices(cross, assetType);
    }

    public static IEnumerable<Price> GetPrices(
        this IDictionary<string, IEnumerable<Price>> prices,
        string cross,
        AssetType? assetType = null)
    {
        return prices.TryGetValue(cross, out var crossPrices)
                    ? assetType.HasValue ? 
                        crossPrices.Where(p => p.AssetType == assetType) :
                        crossPrices
                    : Array.Empty<Price>();
    }

    public static IEnumerable<MarketInfo> GetMarkets(
        this IDictionary<string, IEnumerable<MarketInfo>> markets,
        string cross,
        AssetType? assetType = null)
    {
        return markets.TryGetValue(cross, out var market)
                    ? assetType.HasValue ? 
                        market.Where(p => p.AssetType == assetType) :
                        market
                    : Array.Empty<MarketInfo>();
    }
}