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
    private readonly ITradeExecutionRecorder _tradeExecutionRecorder;
    private readonly ILogger<RebalanceCommand> _logger;
    private readonly string _strategyName;

    public RebalanceCommand(ICalcWeights weightsService, ITradeFactory tradeFactory, IOrderManager orderManager, IYoloBroker broker, IOptions<YoloConfig> options, ILogger<RebalanceCommand> logger)
        : this(weightsService, tradeFactory, orderManager, broker, options.Value, logger)
    {
    }

    public RebalanceCommand(ICalcWeights weightsService, ITradeFactory tradeFactory, IOrderManager orderManager, IYoloBroker broker, YoloConfig yoloConfig, ILogger<RebalanceCommand> logger)
        : this(weightsService, tradeFactory, orderManager, broker, yoloConfig, logger, NoOpTradeExecutionRecorder.Instance, string.Empty)
    {
    }

    public RebalanceCommand(
        ICalcWeights weightsService,
        ITradeFactory tradeFactory,
        IOrderManager orderManager,
        IYoloBroker broker,
        YoloConfig yoloConfig,
        ILogger<RebalanceCommand> logger,
        ITradeExecutionRecorder tradeExecutionRecorder,
        string strategyName)
    {
        ArgumentNullException.ThrowIfNull(weightsService, nameof(weightsService));
        ArgumentNullException.ThrowIfNull(tradeFactory, nameof(tradeFactory));
        ArgumentNullException.ThrowIfNull(orderManager, nameof(orderManager));
        ArgumentNullException.ThrowIfNull(broker, nameof(broker));
        ArgumentNullException.ThrowIfNull(yoloConfig, nameof(yoloConfig));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(tradeExecutionRecorder, nameof(tradeExecutionRecorder));

        _weightsService = weightsService;
        _tradeFactory = tradeFactory;
        _orderManager = orderManager;
        _broker = broker;
        _yoloConfig = yoloConfig;
        _tradeExecutionRecorder = tradeExecutionRecorder;
        _logger = logger;
        _strategyName = strategyName;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting rebalance operation");

        var orders = await _broker.GetOpenOrdersAsync(cancellationToken);

        if (orders.Count != 0)
        {
            if (_yoloConfig.KillOpenOrders)
            {
                _logger.CancelledOrders(orders.Values);

                foreach (var order in orders.Values)
                {
                    await _broker.CancelOrderAsync(order, cancellationToken);
                }
            }
            else
            {
                _logger.OpenOrders(orders.Values);

                return;
            }
        }

        var positions = await _broker.GetPositionsAsync(cancellationToken);
        var weights = await _weightsService.CalculateWeightsAsync(cancellationToken);

        var baseAssetFilter = positions
            .Keys
            .Union(weights.Keys.Select(x => x.GetBaseAndQuoteAssets().BaseAsset))
            .ToHashSet();

        var markets = await _broker.GetMarketsAsync(
            baseAssetFilter,
            _yoloConfig.BaseAsset,
            _yoloConfig.AssetPermissions,
            cancellationToken);

        var trades = _tradeFactory
            .CalculateTrades(weights, positions, markets)
            .Select(EnsureClientOrderId)
            .OrderBy(trade => trade.Symbol)
            .ToArray();

        if (trades.Length == 0)
        {
            _logger.LogInformation("Nothing to do.");

            return;
        }

        var settings = new OrderManagementSettings(TimeSpan.Parse(_yoloConfig.UnfilledOrderTimeout), _yoloConfig.MaxRepriceRetries);
        var advisor = new TradeAdvisor(weights, _tradeFactory, _broker, _yoloConfig.BaseAsset, _yoloConfig.AssetPermissions);
        var runId = Guid.NewGuid().ToString("N");
        var accountContext = _broker.GetAccountContext();
        var tradesByExecutionId = trades
            .Where(trade => !string.IsNullOrWhiteSpace(trade.ClientOrderId))
            .ToDictionary(trade => trade.ClientOrderId!, StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("Order management settings: {Settings}, Advisor={AdvisorType}", settings, advisor.GetType().Name);

        try
        {
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

                if (TryBuildTradeExecutionRecord(
                        update,
                        tradesByExecutionId,
                        positions,
                        markets,
                        accountContext,
                        runId,
                        out var record))
                {
                    try
                    {
                        await _tradeExecutionRecorder.RecordAsync(record, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to record trade execution telemetry");
                    }
                }
            }
        }
        catch (ChannelClosedException)
        {
            // Expected cancellation, we can ignore this
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation, we can ignore this
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during order management");
        }
    }

    private static Trade EnsureClientOrderId(Trade trade) =>
        string.IsNullOrWhiteSpace(trade.ClientOrderId)
            ? trade with { ClientOrderId = "0x" + Guid.NewGuid().ToString("N") }
            : trade;

    private bool TryBuildTradeExecutionRecord(
        OrderUpdate update,
        IReadOnlyDictionary<string, Trade> tradesByExecutionId,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets,
        BrokerAccountContext accountContext,
        string runId,
        out TradeExecutionRecord record)
    {
        record = new TradeExecutionRecord();

        var executionId = update.Order?.ClientId;
        if (string.IsNullOrWhiteSpace(executionId) ||
            !tradesByExecutionId.TryGetValue(executionId, out var trade))
        {
            return false;
        }

        var currentPosition = FindCurrentPosition(trade, positions);
        var targetPosition = currentPosition + trade.Amount;
        var market = FindMarket(trade, markets);
        var isCompleted = update.Type is
            OrderUpdateType.Filled or
            OrderUpdateType.Cancelled or
            OrderUpdateType.TimedOut or
            OrderUpdateType.Error;

        record = new TradeExecutionRecord
        {
            ExecutionId = executionId,
            RunId = runId,
            StrategyName = _strategyName,
            WalletAddress = accountContext.Address,
            VaultAddress = accountContext.VaultAddress,
            Coin = trade.Symbol,
            Side = trade.OrderSide.ToString(),
            TargetPosition = targetPosition,
            CurrentPosition = currentPosition,
            IntendedDelta = trade.Amount,
            ArrivalMid = market?.Mid,
            ArrivalBid = market?.Bid,
            ArrivalAsk = market?.Ask,
            SpreadBps = CalculateSpreadBps(market),
            OrderId = update.Order?.Id.ToString(),
            OrderType = trade.OrderType.ToString(),
            PostOnly = trade.PostPrice,
            ReduceOnly = trade.ReduceOnly,
            LimitPrice = trade.LimitPrice,
            SubmittedAt = update.Order?.Created,
            FilledQty = update.Order?.Filled,
            CancelledQty = update.Order is not null &&
                           (update.Type is OrderUpdateType.Cancelled or OrderUpdateType.TimedOut)
                ? Math.Max(0m, update.Order.Amount - update.Order.Filled.GetValueOrDefault())
                : null,
            CompletedAt = isCompleted ? DateTimeOffset.UtcNow : null,
            Status = update.Type.ToString(),
            Error = IsFailureUpdate(update.Type)
                ? update.Error?.Message ?? update.Message
                : null
        };

        return true;
    }

    private static bool IsFailureUpdate(OrderUpdateType updateType) =>
        updateType is OrderUpdateType.Error or OrderUpdateType.Cancelled or OrderUpdateType.TimedOut;

    private static decimal FindCurrentPosition(
        Trade trade,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions)
    {
        return positions
            .Values
            .SelectMany(x => x)
            .Where(position => position.AssetType == trade.AssetType && position.AssetName == trade.Symbol)
            .Sum(position => position.Amount);
    }

    private static MarketInfo? FindMarket(
        Trade trade,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets)
    {
        return markets
            .Values
            .SelectMany(x => x)
            .FirstOrDefault(market => market.AssetType == trade.AssetType && market.Name == trade.Symbol);
    }

    private static decimal? CalculateSpreadBps(MarketInfo? market)
    {
        if (market?.Spread is not { } spread || market.Mid is not { } mid || mid == 0)
            return null;

        return spread / mid * 10_000m;
    }
}
