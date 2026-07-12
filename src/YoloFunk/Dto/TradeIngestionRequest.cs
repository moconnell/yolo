namespace YoloFunk.Dto;

public sealed record TradeIngestionRequest(string StrategyKey, string Trigger, DateTime RequestedAtUtc);
