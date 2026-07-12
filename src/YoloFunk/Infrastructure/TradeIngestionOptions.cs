namespace YoloFunk.Infrastructure;

public sealed class TradeIngestionOptions
{
    public DateTimeOffset StartUtc { get; init; } = DateTimeOffset.Parse("2020-01-01T00:00:00+00:00");

    public int WindowDays { get; init; } = 1;

    public int OverlapDays { get; init; } = 2;
}
