using System.Text.Json;
using System.Threading.Channels;

using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloApp.Extensions;
using YoloBroker.Interface;
using YoloTrades;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace YoloApp.Commands;

public class RebalanceCommand : ICommand
{
    private readonly ICalcWeights _weightsService;
    private readonly ITradeFactory _tradeFactory;
    private readonly IOrderManager _orderManager;
    private readonly IYoloBroker _broker;
    private readonly YoloConfig _yoloConfig;
    private readonly IRebalanceEventRecorder _rebalanceEventRecorder;
    private readonly ILogger<RebalanceCommand> _logger;
    private readonly string _strategyName;
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);

    public RebalanceCommand(ICalcWeights weightsService, ITradeFactory tradeFactory, IOrderManager orderManager, IYoloBroker broker, IOptions<YoloConfig> options, ILogger<RebalanceCommand> logger)
        : this(weightsService, tradeFactory, orderManager, broker, options.Value, logger)
    {
    }

    public RebalanceCommand(ICalcWeights weightsService, ITradeFactory tradeFactory, IOrderManager orderManager, IYoloBroker broker, YoloConfig yoloConfig, ILogger<RebalanceCommand> logger)
        : this(weightsService, tradeFactory, orderManager, broker, yoloConfig, logger, NoOpRebalanceEventRecorder.Instance, string.Empty)
    {
    }

    public RebalanceCommand(
        ICalcWeights weightsService,
        ITradeFactory tradeFactory,
        IOrderManager orderManager,
        IYoloBroker broker,
        YoloConfig yoloConfig,
        ILogger<RebalanceCommand> logger,
        string strategyName)
        : this(weightsService, tradeFactory, orderManager, broker, yoloConfig, logger, NoOpRebalanceEventRecorder.Instance, strategyName)
    {
    }

    public RebalanceCommand(
        ICalcWeights weightsService,
        ITradeFactory tradeFactory,
        IOrderManager orderManager,
        IYoloBroker broker,
        YoloConfig yoloConfig,
        ILogger<RebalanceCommand> logger,
        IRebalanceEventRecorder rebalanceEventRecorder,
        string strategyName)
    {
        ArgumentNullException.ThrowIfNull(weightsService, nameof(weightsService));
        ArgumentNullException.ThrowIfNull(tradeFactory, nameof(tradeFactory));
        ArgumentNullException.ThrowIfNull(orderManager, nameof(orderManager));
        ArgumentNullException.ThrowIfNull(broker, nameof(broker));
        ArgumentNullException.ThrowIfNull(yoloConfig, nameof(yoloConfig));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(rebalanceEventRecorder, nameof(rebalanceEventRecorder));

        _weightsService = weightsService;
        _tradeFactory = tradeFactory;
        _orderManager = orderManager;
        _broker = broker;
        _yoloConfig = yoloConfig;
        _rebalanceEventRecorder = rebalanceEventRecorder;
        _logger = logger;
        _strategyName = strategyName;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting rebalance operation");
        var runId = Guid.NewGuid().ToString("N");
        var accountContext = _broker.GetAccountContext() ?? new BrokerAccountContext(null, null);
        var eventSequence = 0;

        try
        {
            await RecordRebalanceEventAsync(
                runId,
                accountContext,
                ++eventSequence,
                "RunStarted",
                "Rebalance run started",
                new
                {
                    _yoloConfig.BaseAsset,
                    _yoloConfig.AssetPermissions,
                    _yoloConfig.KillOpenOrders,
                    _yoloConfig.RebalanceMode
                },
                cancellationToken: cancellationToken);

            var orders = await _broker.GetOpenOrdersAsync(cancellationToken);

            if (orders.Count != 0)
            {
                await RecordRebalanceEventAsync(
                    runId,
                    accountContext,
                    ++eventSequence,
                    "OpenOrdersFound",
                    $"Found {orders.Count} open order(s)",
                    new { OrderIds = orders.Keys.ToArray() },
                    cancellationToken: cancellationToken);

                if (_yoloConfig.KillOpenOrders)
                {
                    _logger.CancelledOrders(orders.Values);

                    foreach (var order in orders.Values)
                    {
                        await _broker.CancelOrderAsync(order, cancellationToken);
                    }

                    await RecordRebalanceEventAsync(
                        runId,
                        accountContext,
                        ++eventSequence,
                        "OpenOrdersCancelled",
                        $"Cancelled {orders.Count} open order(s)",
                        orders.Values.Select(ToOrderEventPayload).ToArray(),
                        cancellationToken: cancellationToken);
                }
                else
                {
                    _logger.OpenOrders(orders.Values);
                    await RecordRebalanceEventAsync(
                        runId,
                        accountContext,
                        ++eventSequence,
                        "RunSkippedOpenOrders",
                        "Rebalance skipped because open orders exist",
                        orders.Values.Select(ToOrderEventPayload).ToArray(),
                        level: "Warning",
                        cancellationToken: cancellationToken);

                    return;
                }
            }

            var positions = await _broker.GetPositionsAsync(cancellationToken);
            await RecordRebalanceEventAsync(
                runId,
                accountContext,
                ++eventSequence,
                "PositionsFetched",
                $"Fetched {positions.Values.Sum(x => x.Count)} position(s)",
                positions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(ToPositionEventPayload).ToArray()),
                cancellationToken: cancellationToken);

            var weights = await _weightsService.CalculateWeightsAsync(cancellationToken);
            await RecordRebalanceEventAsync(
                runId,
                accountContext,
                ++eventSequence,
                "WeightsCalculated",
                $"Calculated {weights.Count} target weight(s)",
                weights,
                cancellationToken: cancellationToken);

            var baseAssetFilter = positions
                .Keys
                .Union(weights.Keys.Select(x => x.GetBaseAndQuoteAssets().BaseAsset))
                .ToHashSet();

            var markets = await _broker.GetMarketsAsync(
                baseAssetFilter,
                _yoloConfig.BaseAsset,
                _yoloConfig.AssetPermissions,
                cancellationToken);
            await RecordRebalanceEventAsync(
                runId,
                accountContext,
                ++eventSequence,
                "MarketsFetched",
                $"Fetched {markets.Values.Sum(x => x.Count)} market(s)",
                markets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(ToMarketEventPayload).ToArray()),
                cancellationToken: cancellationToken);

            var trades = _tradeFactory
                .CalculateTrades(weights, positions, markets)
                .Select(EnsureClientOrderId)
                .OrderBy(trade => trade.Symbol)
                .ToArray();
            await RecordRebalanceEventAsync(
                runId,
                accountContext,
                ++eventSequence,
                "RebalancePlanCalculated",
                $"Calculated {trades.Length} trade(s)",
                trades.Select(ToTradeEventPayload).ToArray(),
                cancellationToken: cancellationToken);

            if (trades.Length == 0)
            {
                _logger.LogInformation("Nothing to do.");
                await RecordRebalanceEventAsync(
                    runId,
                    accountContext,
                    ++eventSequence,
                    "RunCompleted",
                    "Rebalance run completed with no trades",
                    new { TradeCount = 0 },
                    cancellationToken: cancellationToken);

                return;
            }

            var settings = new OrderManagementSettings(TimeSpan.Parse(_yoloConfig.UnfilledOrderTimeout), _yoloConfig.MaxRepriceRetries);
            var advisor = new TradeAdvisor(weights, _tradeFactory, _broker, _yoloConfig.BaseAsset, _yoloConfig.AssetPermissions);
            var tradesByExecutionId = trades
                .Where(trade => !string.IsNullOrWhiteSpace(trade.ClientOrderId))
                .ToDictionary(trade => trade.ClientOrderId!, StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Order management settings: {Settings}, Advisor={AdvisorType}", settings, advisor.GetType().Name);
            foreach (var trade in trades)
            {
                await RecordRebalanceEventAsync(
                    runId,
                    accountContext,
                    ++eventSequence,
                    "TradeProposed",
                    $"{trade.OrderSide} {trade.Symbol} {trade.AbsoluteAmount}",
                    ToTradeEventPayload(trade),
                    coin: trade.Symbol,
                    clientOrderId: trade.ClientOrderId,
                    cancellationToken: cancellationToken);
            }

            await foreach (var update in _orderManager.ManageOrdersAsync(trades, settings, advisor, cancellationToken))
            {
                if (update.Type == OrderUpdateType.Error)
                {
                    _logger.OrderError(update.Symbol, update.Message, 1);
                }
                else
                {
                    _logger.OrderUpdate(update.Symbol, update.Type, update.Message);
                }

                await RecordRebalanceEventAsync(
                    runId,
                    accountContext,
                    ++eventSequence,
                    update.Type == OrderUpdateType.Error ? "OrderError" : "OrderUpdate",
                    $"{update.Symbol} {update.Type}",
                    ToOrderUpdateEventPayload(update),
                    level: update.Type == OrderUpdateType.Error ? "Error" : "Info",
                    coin: update.Symbol,
                    clientOrderId: update.Order?.ClientId,
                    orderId: update.Order?.Id.ToString(),
                    cancellationToken: cancellationToken);
            }

            await RecordRebalanceEventAsync(
                runId,
                accountContext,
                ++eventSequence,
                "RunCompleted",
                "Rebalance run completed",
                new { TradeCount = trades.Length },
                cancellationToken: cancellationToken);
        }
        catch (ChannelClosedException)
        {
            // Expected cancellation, we can ignore this
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during order management");
            await RecordRebalanceEventAsync(
                runId,
                accountContext,
                ++eventSequence,
                "RunFailed",
                "Unexpected error during order management",
                new { ex.Message, ExceptionType = ex.GetType().FullName },
                level: "Error",
                cancellationToken: cancellationToken);
        }
    }

    private async Task RecordRebalanceEventAsync(
        string runId,
        BrokerAccountContext accountContext,
        int sequence,
        string eventType,
        string summary,
        object? payload,
        string level = "Info",
        string? coin = null,
        string? clientOrderId = null,
        string? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var record = new RebalanceEventRecord
        {
            RunId = runId,
            StrategyName = _strategyName,
            TimestampUtc = DateTimeOffset.UtcNow,
            Sequence = sequence,
            EventType = eventType,
            Level = level,
            Summary = summary,
            WalletAddress = accountContext.Address,
            VaultAddress = accountContext.VaultAddress,
            Coin = coin,
            ClientOrderId = clientOrderId,
            OrderId = orderId,
            PayloadJson = payload is null ? "{}" : JsonSerializer.Serialize(payload, EventJsonOptions)
        };

        try
        {
            await _rebalanceEventRecorder.RecordAsync(record, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Failed to record rebalance event {EventType}", eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record rebalance event {EventType}", eventType);
        }
    }

    private static Trade EnsureClientOrderId(Trade trade) =>
        string.IsNullOrWhiteSpace(trade.ClientOrderId)
            ? trade with { ClientOrderId = "0x" + Guid.NewGuid().ToString("N") }
            : trade;

    private static object ToOrderEventPayload(Order order) => new
    {
        order.Id,
        order.Symbol,
        order.AssetType,
        order.Created,
        order.OrderSide,
        order.OrderStatus,
        order.Amount,
        order.Filled,
        order.LimitPrice,
        order.ClientId
    };

    private static object ToOrderUpdateEventPayload(OrderUpdate update) => new
    {
        update.Symbol,
        update.Type,
        update.Message,
        Error = update.Error?.Message,
        Order = update.Order is null ? null : ToOrderEventPayload(update.Order)
    };

    private static object ToPositionEventPayload(Position position) => new
    {
        position.AssetName,
        position.BaseAsset,
        position.AssetType,
        position.Amount
    };

    private static object ToMarketEventPayload(MarketInfo market) => new
    {
        market.Name,
        market.BaseAsset,
        market.QuoteAsset,
        market.AssetType,
        market.TimeStamp,
        market.PriceStep,
        market.QuantityStep,
        market.MinProvideSize,
        market.Ask,
        market.Bid,
        market.Last,
        market.Mid,
        market.Expiry,
        market.Spread
    };

    private static object ToTradeEventPayload(Trade trade) => new
    {
        trade.Symbol,
        trade.AssetType,
        trade.Amount,
        trade.AbsoluteAmount,
        trade.OrderSide,
        trade.LimitPrice,
        trade.OrderType,
        trade.PostPrice,
        trade.ReduceOnly,
        trade.Expiry,
        trade.ClientOrderId
    };
}
