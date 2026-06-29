using Azure;
using Azure.Data.Tables;
using YoloAbstractions;
using YoloAbstractions.Interfaces;

namespace YoloFunk.Infrastructure;

public sealed class AzureTableRebalanceEventRecorder(TableServiceClient tableServiceClient) : IRebalanceEventRecorder
{
    private const string TableName = "rebalanceevents";
    private readonly TableClient _tableClient = tableServiceClient.GetTableClient(TableName);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public async Task RecordAsync(RebalanceEventRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await EnsureInitializedAsync(cancellationToken);

        var partitionKey = SanitizeTableKey($"{record.StrategyName}|{record.RunId}");
        var rowKey = SanitizeTableKey($"{record.TimestampUtc:yyyyMMddHHmmssfffffff}|{record.Sequence:D6}|{record.EventType}");
        var entity = new TableEntity(partitionKey, rowKey)
        {
            ["RunId"] = record.RunId,
            ["StrategyName"] = record.StrategyName,
            ["TimestampUtc"] = record.TimestampUtc,
            ["Sequence"] = record.Sequence,
            ["EventType"] = record.EventType,
            ["Level"] = record.Level,
            ["Summary"] = record.Summary,
            ["PayloadJson"] = record.PayloadJson
        };

        AddIfNotNull(entity, "WalletAddress", record.WalletAddress);
        AddIfNotNull(entity, "VaultAddress", record.VaultAddress);
        AddIfNotNull(entity, "Coin", record.Coin);
        AddIfNotNull(entity, "ClientOrderId", record.ClientOrderId);
        AddIfNotNull(entity, "OrderId", record.OrderId);

        await _tableClient.AddEntityAsync(entity, cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await _tableClient.CreateIfNotExistsAsync(cancellationToken);
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static void AddIfNotNull(TableEntity entity, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entity[name] = value;
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
