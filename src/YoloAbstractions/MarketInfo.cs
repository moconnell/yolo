using System;

namespace YoloAbstractions;

public record MarketInfo(
    string Name,
    string BaseAsset,
    string? QuoteAsset,
    AssetType AssetType,
    decimal PriceStep,
    decimal QuantityStep,
    decimal? Ask,
    decimal? Bid,
    decimal? Last,
    DateTime? Expiry,
    DateTime TimeStamp)
{
    public string Key => $"{Name}-{AssetType}";
}