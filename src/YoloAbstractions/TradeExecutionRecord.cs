using System.Globalization;

namespace YoloAbstractions;

public sealed record TradeExecutionRecord
{
    public string ExecutionId { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string StrategyName { get; init; } = string.Empty;
    public string? WalletAddress { get; init; }
    public string? VaultAddress { get; init; }
    public string Coin { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public decimal? TargetPosition { get; init; }
    public decimal? CurrentPosition { get; init; }
    public decimal? IntendedDelta { get; init; }
    public decimal? ArrivalMid { get; init; }
    public decimal? ArrivalBid { get; init; }
    public decimal? ArrivalAsk { get; init; }
    public decimal? SpreadBps { get; init; }
    public string? OrderId { get; init; }
    public string OrderType { get; init; } = string.Empty;
    public bool? PostOnly { get; init; }
    public bool? ReduceOnly { get; init; }
    public decimal? LimitPrice { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public decimal? FilledQty { get; init; }
    public decimal? AvgFillPrice { get; init; }
    public decimal? Fees { get; init; }
    public decimal? MakerQty { get; init; }
    public decimal? MakerAvgFillPrice { get; init; }
    public decimal? MakerFees { get; init; }
    public decimal? TakerQty { get; init; }
    public decimal? TakerAvgFillPrice { get; init; }
    public decimal? TakerFees { get; init; }
    public decimal? CancelledQty { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }

    public static string? FormatDecimal(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture);
}
