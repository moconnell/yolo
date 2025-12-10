namespace YoloAbstractions;

public record Price(
    string Symbol,
    AssetType AssetType,
    decimal? Ask,
    decimal? Bid,
    decimal? Last,
    DateTime? At);