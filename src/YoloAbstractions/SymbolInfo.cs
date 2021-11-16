namespace YoloAbstractions
{
    public record SymbolInfo(string BaseAsset, string QuoteAsset, AssetType AssetType, decimal LotSizeStepSize)
    {
        public string Symbol => $"{BaseAsset}{QuoteAsset}";
        
        public string Key => $"{Symbol}-{AssetType}";
    }
}