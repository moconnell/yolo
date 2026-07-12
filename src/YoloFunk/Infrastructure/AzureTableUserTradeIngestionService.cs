using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloBroker.Interface;

namespace YoloFunk.Infrastructure;

public sealed class AzureTableUserTradeIngestionService(
    UserTradeIngestionContext context,
    TradeIngestionOptions options,
    IUserTradeSource tradeSource,
    TableServiceClient tableServiceClient,
    ILogger<AzureTableUserTradeIngestionService> logger) : IUserTradeIngestionService
{
    private const string TradesTableName = "usertrades";
    private const string StateTableName = "usertradeingestionstate";
    private readonly TableClient _tradesTable = tableServiceClient.GetTableClient(TradesTableName);
    private readonly TableClient _stateTable = tableServiceClient.GetTableClient(StateTableName);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _ingestionLock = new(1, 1);
    private volatile bool _initialized;

    public async Task<UserTradeIngestionResult> IngestAsync(CancellationToken cancellationToken = default)
    {
        if (!await _ingestionLock.WaitAsync(0, cancellationToken))
        {
            logger.LogInformation(
                "Skipping {Exchange} user trade ingestion for {Strategy}; ingestion is already running",
                context.Exchange,
                context.StrategyName);
            var skippedAtUtc = DateTimeOffset.UtcNow;
            return new UserTradeIngestionResult(context.StrategyName, skippedAtUtc, skippedAtUtc, 0, 0);
        }

        try
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var plan = await PlanAsync(nowUtc, cancellationToken);
            var totalTrades = 0;
            foreach (var window in plan.Windows)
            {
                var result = await IngestWindowAsync(window, cancellationToken);
                totalTrades += result.TradeCount;
            }

            return await CompleteAsync(
                plan.StartUtc,
                plan.EndUtc,
                plan.Windows.Count,
                totalTrades,
                cancellationToken);
        }
        finally
        {
            _ingestionLock.Release();
        }
    }

    public async Task<UserTradeIngestionPlan> PlanAsync(
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var startUtc = await GetNextStartUtcAsync(cancellationToken);
        if (startUtc >= endUtc)
        {
            return new UserTradeIngestionPlan(context.StrategyName, startUtc, endUtc, []);
        }

        var windows = new List<UserTradeIngestionWindow>();
        var windowSize = TimeSpan.FromDays(Math.Max(1, options.WindowDays));

        for (var windowStart = startUtc; windowStart < endUtc; windowStart += windowSize)
        {
            var windowEnd = Min(windowStart + windowSize, endUtc);
            windows.Add(new UserTradeIngestionWindow(windowStart, windowEnd));
        }

        return new UserTradeIngestionPlan(context.StrategyName, startUtc, endUtc, windows);
    }

    public async Task<UserTradeIngestionWindowResult> IngestWindowAsync(
        UserTradeIngestionWindow window,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var trades = await tradeSource.GetUserTradesByTimeAsync(window.StartUtc, window.EndUtc, cancellationToken);
        await UpsertTradesAsync(trades, cancellationToken);

        logger.LogInformation(
            "Ingested {TradeCount} {Exchange} user trades for {Strategy} from {StartUtc} to {EndUtc}",
            trades.Count,
            context.Exchange,
            context.StrategyName,
            window.StartUtc,
            window.EndUtc);

        return new UserTradeIngestionWindowResult(window.StartUtc, window.EndUtc, trades.Count);
    }

    public async Task<UserTradeIngestionResult> CompleteAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int windowCount,
        int tradeCount,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (startUtc < endUtc)
        {
            await SaveStateAsync(endUtc, cancellationToken);
        }

        return new UserTradeIngestionResult(context.StrategyName, startUtc, endUtc, windowCount, tradeCount);
    }

    private async Task<DateTimeOffset> GetNextStartUtcAsync(CancellationToken cancellationToken)
    {
        try
        {
            var state = await _stateTable.GetEntityAsync<TableEntity>(
                GetStatePartitionKey(),
                "watermark",
                cancellationToken: cancellationToken);

            if (state.Value.TryGetValue("LastSyncedThroughUtc", out var value) &&
                value is DateTimeOffset lastSyncedThroughUtc)
            {
                var overlapStart = lastSyncedThroughUtc.AddDays(-Math.Max(0, options.OverlapDays));
                return overlapStart > options.StartUtc ? overlapStart : options.StartUtc;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
        }

        return options.StartUtc;
    }

    private async Task SaveStateAsync(DateTimeOffset lastSyncedThroughUtc, CancellationToken cancellationToken)
    {
        var entity = new TableEntity(GetStatePartitionKey(), "watermark")
        {
            ["StrategyName"] = context.StrategyName,
            ["Exchange"] = context.Exchange.ToString(),
            ["Network"] = context.Network,
            ["Address"] = context.Address,
            ["LastSyncedThroughUtc"] = lastSyncedThroughUtc
        };
        AddIfNotNull(entity, "VaultAddress", context.VaultAddress);

        await _stateTable.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    private TableEntity ToEntity(UserTradeRecord trade)
    {
        var entity = new TableEntity(GetTradePartitionKey(), BuildTradeRowKey(trade))
        {
            ["StrategyName"] = context.StrategyName,
            ["Exchange"] = trade.Exchange.ToString(),
            ["Network"] = context.Network,
            ["Address"] = context.Address,
            ["TimestampUtc"] = trade.TimestampUtc,
            ["ExchangeSymbol"] = trade.ExchangeSymbol,
            ["Price"] = trade.Price,
            ["Quantity"] = trade.Quantity,
            ["Fee"] = trade.Fee,
            ["Crossed"] = trade.Crossed,
            ["RawJson"] = trade.RawJson
        };

        AddIfNotNull(entity, "VaultAddress", context.VaultAddress);
        AddIfNotNull(entity, "ClosedPnl", trade.ClosedPnl);
        AddIfNotNull(entity, "Symbol", trade.Symbol);
        AddIfNotNull(entity, "SymbolType", trade.SymbolType);
        AddIfNotNull(entity, "Direction", trade.Direction);
        AddIfNotNull(entity, "Hash", trade.Hash);
        AddIfNotNull(entity, "OrderId", trade.OrderId);
        AddIfNotNull(entity, "OrderSide", trade.OrderSide);
        AddIfNotNull(entity, "StartPosition", trade.StartPosition);
        AddIfNotNull(entity, "FeeToken", trade.FeeToken);
        AddIfNotNull(entity, "BuilderFee", trade.BuilderFee);
        AddIfNotNull(entity, "TradeId", trade.TradeId);
        AddIfNotNull(entity, "LiquidationJson", trade.LiquidationJson);
        AddIfNotNull(entity, "TwapId", trade.TwapId);
        AddIfNotNull(entity, "ClientOrderId", trade.ClientOrderId);

        return entity;
    }

    private async Task UpsertTradesAsync(
        IReadOnlyCollection<UserTradeRecord> trades,
        CancellationToken cancellationToken)
    {
        if (trades.Count == 0)
        {
            return;
        }

        var entities = trades.Select(ToEntity);
        foreach (var partition in entities.GroupBy(entity => entity.PartitionKey))
        {
            foreach (var batch in partition.Chunk(100))
            {
                var actions = batch
                    .Select(entity => new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity))
                    .ToArray();

                await _tradesTable.SubmitTransactionAsync(actions, cancellationToken);
            }
        }
    }

    private string GetTradePartitionKey()
    {
        var addressKey = string.IsNullOrWhiteSpace(context.VaultAddress)
            ? context.Address
            : context.VaultAddress;

        return SanitizeTableKey($"{context.StrategyName}|{context.Exchange}|{context.Network}|{addressKey}");
    }

    private string GetStatePartitionKey() => GetTradePartitionKey();

    private static string BuildTradeRowKey(UserTradeRecord trade)
    {
        var identity = trade.TradeId?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? trade.ClientOrderId
            ?? trade.OrderId?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? trade.Hash
            ?? $"{trade.ExchangeSymbol}|{trade.Price}|{trade.Quantity}|{BuildRawJsonHash(trade.RawJson)}";

        return SanitizeTableKey($"{trade.TimestampUtc:yyyyMMddHHmmssfffffff}|{identity}");
    }

    private static string BuildRawJsonHash(string rawJson)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawJson));
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
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
                await Task.WhenAll(
                    _tradesTable.CreateIfNotExistsAsync(cancellationToken),
                    _stateTable.CreateIfNotExistsAsync(cancellationToken));
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

    private static void AddIfNotNull(TableEntity entity, string name, long? value)
    {
        if (value.HasValue)
        {
            entity[name] = value.Value;
        }
    }

    private static DateTimeOffset Min(DateTimeOffset one, DateTimeOffset two) => one < two ? one : two;

    private static string SanitizeTableKey(string value)
    {
        return value
            .Replace("/", "|")
            .Replace("\\", "|")
            .Replace("#", "_")
            .Replace("?", "_");
    }
}
