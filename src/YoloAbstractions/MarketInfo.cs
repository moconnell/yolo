using System;

namespace YoloAbstractions;

public record MarketInfo(
    string Name,
    string BaseAsset,
    string? QuoteAsset,
    AssetType AssetType,
    DateTime TimeStamp,
    decimal? PriceStep = null,
    decimal? QuantityStep = null,
    decimal? MinProvideSize = null,
    decimal? Ask = null,
    decimal? Bid = null,
    decimal? Last = null,
    decimal? Mid = null,
    DateTime? Expiry = null)
{
    public string Key => $"{Name}-{AssetType}";
}