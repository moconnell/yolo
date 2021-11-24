using System;
using FTX.Net.Enums;
using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions
{
    public static class SymbolTypeExtensions
    {
        public static AssetType ToAssetType(this SymbolType type) => type switch
        {
            SymbolType.Future => AssetType.Future,
            SymbolType.Spot => AssetType.Spot,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}