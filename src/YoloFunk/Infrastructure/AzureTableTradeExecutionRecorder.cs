using Azure;
using Azure.Data.Tables;
using YoloAbstractions;
using YoloAbstractions.Interfaces;

namespace YoloFunk.Infrastructure;

public sealed class AzureTableTradeExecutionRecorder(TableServiceClient tableServiceClient) : ITradeExecutionRecorder
{
    private const string TableName = "tradeexecutions";

    public async Task RecordAsync(TradeExecutionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var tableClient = tableServiceClient.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = new TableEntity(
            SanitizeTableKey(string.IsNullOrWhiteSpace(record.StrategyName) ? "unknown" : record.StrategyName),
            SanitizeTableKey($"{record.RunId}|{record.ExecutionId}"))
        {
            ["ExecutionId"] = record.ExecutionId,
            ["RunId"] = record.RunId,
            ["StrategyName"] = record.StrategyName,
            ["Coin"] = record.Coin,
            ["Side"] = record.Side,
            ["OrderType"] = record.OrderType,
            ["RecordedAt"] = DateTimeOffset.UtcNow
        };

        AddIfNotNull(entity, "WalletAddress", record.WalletAddress);
        AddIfNotNull(entity, "VaultAddress", record.VaultAddress);
        AddIfNotNull(entity, "TargetPosition", TradeExecutionRecord.FormatDecimal(record.TargetPosition));
        AddIfNotNull(entity, "CurrentPosition", TradeExecutionRecord.FormatDecimal(record.CurrentPosition));
        AddIfNotNull(entity, "IntendedDelta", TradeExecutionRecord.FormatDecimal(record.IntendedDelta));
        AddIfNotNull(entity, "ArrivalMid", TradeExecutionRecord.FormatDecimal(record.ArrivalMid));
        AddIfNotNull(entity, "ArrivalBid", TradeExecutionRecord.FormatDecimal(record.ArrivalBid));
        AddIfNotNull(entity, "ArrivalAsk", TradeExecutionRecord.FormatDecimal(record.ArrivalAsk));
        AddIfNotNull(entity, "SpreadBps", TradeExecutionRecord.FormatDecimal(record.SpreadBps));
        AddIfNotNull(entity, "OrderId", record.OrderId);
        AddIfNotNull(entity, "PostOnly", record.PostOnly);
        AddIfNotNull(entity, "ReduceOnly", record.ReduceOnly);
        AddIfNotNull(entity, "LimitPrice", TradeExecutionRecord.FormatDecimal(record.LimitPrice));
        AddIfNotNull(entity, "SubmittedAt", record.SubmittedAt);
        AddIfNotNull(entity, "FilledQty", TradeExecutionRecord.FormatDecimal(record.FilledQty));
        AddIfNotNull(entity, "AvgFillPrice", TradeExecutionRecord.FormatDecimal(record.AvgFillPrice));
        AddIfNotNull(entity, "Fees", TradeExecutionRecord.FormatDecimal(record.Fees));
        AddIfNotNull(entity, "MakerQty", TradeExecutionRecord.FormatDecimal(record.MakerQty));
        AddIfNotNull(entity, "MakerAvgFillPrice", TradeExecutionRecord.FormatDecimal(record.MakerAvgFillPrice));
        AddIfNotNull(entity, "MakerFees", TradeExecutionRecord.FormatDecimal(record.MakerFees));
        AddIfNotNull(entity, "TakerQty", TradeExecutionRecord.FormatDecimal(record.TakerQty));
        AddIfNotNull(entity, "TakerAvgFillPrice", TradeExecutionRecord.FormatDecimal(record.TakerAvgFillPrice));
        AddIfNotNull(entity, "TakerFees", TradeExecutionRecord.FormatDecimal(record.TakerFees));
        AddIfNotNull(entity, "CancelledQty", TradeExecutionRecord.FormatDecimal(record.CancelledQty));
        AddIfNotNull(entity, "CompletedAt", record.CompletedAt);
        AddIfNotNull(entity, "Status", record.Status);
        AddIfNotNull(entity, "Error", record.Error);

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    private static void AddIfNotNull(TableEntity entity, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entity[name] = value;
        }
    }

    private static void AddIfNotNull(TableEntity entity, string name, bool? value)
    {
        if (value.HasValue)
        {
            entity[name] = value.Value;
        }
    }

    private static void AddIfNotNull(TableEntity entity, string name, DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            entity[name] = value.Value;
        }
    }

    private static string SanitizeTableKey(string value)
    {
        return value
            .Replace("/", "|")
            .Replace("\\", "|")
            .Replace("#", "_")
            .Replace("?", "_");
    }
}
