namespace YoloFunk.Dto;

public sealed record RebalanceRequest(string StrategyKey, string Trigger, DateTime RequestedAtUtc);
