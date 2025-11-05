namespace YoloAbstractions;

public enum RebalanceMode
{
    /// <summary>
    /// Rebalance to the center of the tolerance band (ideal weight).
    /// This is the default behavior.
    /// </summary>
    Center,

    /// <summary>
    /// Rebalance to the nearest edge of the tolerance band.
    /// This minimizes trading when price is equally likely to reverse.
    /// </summary>
    Edge
}
