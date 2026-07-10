namespace YoloFunk.Infrastructure;

public interface IUserTradeIngestionService
{
    Task<UserTradeIngestionResult> IngestAsync(CancellationToken cancellationToken = default);

    Task<UserTradeIngestionPlan> PlanAsync(
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default);

    Task<UserTradeIngestionWindowResult> IngestWindowAsync(
        UserTradeIngestionWindow window,
        CancellationToken cancellationToken = default);

    Task<UserTradeIngestionResult> CompleteAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int windowCount,
        int tradeCount,
        CancellationToken cancellationToken = default);
}

public sealed record UserTradeIngestionPlan(
    string StrategyName,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<UserTradeIngestionWindow> Windows);

public sealed record UserTradeIngestionWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public sealed record UserTradeIngestionWindowResult(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int TradeCount);

public sealed record UserTradeIngestionResult(
    string StrategyName,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int WindowCount,
    int TradeCount);
