using FTX.Net.Enums;
using YoloAbstractions;

namespace YoloBroker.Ftx.Extensions;

public static partial class FtxExtensions
{
    public static AssetType ToAssetType(this SymbolType type)
    {
        return type switch
        {
            SymbolType.Future => AssetType.Future,
            SymbolType.Spot => AssetType.Spot,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}