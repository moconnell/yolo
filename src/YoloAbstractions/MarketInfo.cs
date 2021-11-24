using System;

namespace YoloAbstractions
{
    public record MarketInfo(
        string Name,
        string BaseAsset,
        string? QuoteAsset,
        AssetType AssetType,
        decimal LotSizeStep,
        decimal? Ask,
        decimal? Bid,
        decimal? Last,
        DateTime TimeStamp)
    {
        public string Key => $"{Name}-{AssetType}";
    }
}