using System;

namespace YoloAbstractions
{
    [Flags]
    public enum AssetPermissions
    {
        None = 0,
        Spot = 1,
        PerpetualFutures = 2,
        SpotAndPerp = 3,
        ExpiringFutures = 4,
        All = 7
    }
}