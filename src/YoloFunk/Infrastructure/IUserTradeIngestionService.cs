namespace YoloFunk.Infrastructure;

public interface IUserTradeIngestionService
{
    Task<UserTradeIngestionResult> IngestAsync(CancellationToken cancellationToken = default);
}

public sealed record UserTradeIngestionResult(
    string StrategyName,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int WindowCount,
    int TradeCount);
