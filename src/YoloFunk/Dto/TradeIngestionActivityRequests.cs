namespace YoloFunk.Dto;

public sealed record TradeIngestionPlanActivityRequest(
    string StrategyKey,
    string Trigger,
    DateTime RequestedAtUtc,
    DateTimeOffset EndUtc);

public sealed record TradeIngestionWindowActivityRequest(
    string StrategyKey,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc);

public sealed record TradeIngestionCompleteActivityRequest(
    string StrategyKey,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int WindowCount,
    int TradeCount);
