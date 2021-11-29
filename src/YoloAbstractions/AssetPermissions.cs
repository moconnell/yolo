using System;

namespace YoloAbstractions
{
    [Flags]
    public enum AssetPermissions
    {
        None = 0,
        LongSpot = 1,
        ShortSpot = 2,
        Spot = 3,
        PerpetualFutures = 4,
        LongSpotAndPerp = 5,
        SpotAndPerp = 7,
        ExpiringFutures = 8,
        All = 15
    }
}