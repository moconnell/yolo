using System;
using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

public static class PriceExtensions
{
    public static IEnumerable<Price> GetPrices(
        this IDictionary<string, IEnumerable<Price>> prices,
        string token,
        string baseCurrencyToken,
        AssetType? assetType = null)
    {
        var cross = $"{token}{baseCurrencyToken}";
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
}