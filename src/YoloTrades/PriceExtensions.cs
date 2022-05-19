using System;
using System.Collections.Generic;
using YoloAbstractions;

public static class PriceExtensions
{
    public static IEnumerable<MarketInfo> GetMarkets(
        this IDictionary<string, IEnumerable<MarketInfo>> markets,
        string token) =>
        markets.TryGetValue(token, out var tokenMarkets)
            ? tokenMarkets
            : Array.Empty<MarketInfo>();
}