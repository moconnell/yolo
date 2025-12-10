namespace YoloAbstractions;

public record OrderManagementSettings(
    TimeSpan UnfilledOrderTimeout = default,
    bool SwitchToMarketOnTimeout = true,
    TimeSpan StatusCheckInterval = default)
{
    public static OrderManagementSettings Default => new(
        UnfilledOrderTimeout: TimeSpan.FromMinutes(5),
        SwitchToMarketOnTimeout: true,
        StatusCheckInterval: TimeSpan.FromSeconds(30));
}