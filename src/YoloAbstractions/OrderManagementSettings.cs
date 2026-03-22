namespace YoloAbstractions;

public record OrderManagementSettings(TimeSpan UnfilledOrderTimeout, int MaxRepriceRetries);
