using Microsoft.Extensions.Logging;

using System.Threading.Channels;

using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloApp.Extensions;
using YoloBroker.Interface;
using Microsoft.Extensions.Options;

namespace YoloApp.Commands;

public class RebalanceCommand : ICommand
{
    private readonly ICalcWeights _weightsService;
    private readonly ITradeFactory _tradeFactory;
    private readonly IYoloBroker _broker;
    private readonly YoloConfig _yoloConfig;
    private readonly ILogger<RebalanceCommand> _logger;
    public RebalanceCommand(ICalcWeights weightsService, ITradeFactory tradeFactory, IYoloBroker broker, IOptions<YoloConfig> options, ILogger<RebalanceCommand> logger)
        : this(weightsService, tradeFactory, broker, options.Value, logger)
    {
    }

    public RebalanceCommand(ICalcWeights weightsService, ITradeFactory tradeFactory, IYoloBroker broker, YoloConfig yoloConfig, ILogger<RebalanceCommand> logger)
    {
        ArgumentNullException.ThrowIfNull(weightsService, nameof(weightsService));
        ArgumentNullException.ThrowIfNull(tradeFactory, nameof(tradeFactory));
        ArgumentNullException.ThrowIfNull(broker, nameof(broker));
        ArgumentNullException.ThrowIfNull(yoloConfig, nameof(yoloConfig));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _weightsService = weightsService;
        _tradeFactory = tradeFactory;
        _broker = broker;
        _yoloConfig = yoloConfig;
        _logger = logger;
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
            .OrderBy(trade => trade.Symbol)
            .ToArray();

        if (trades.Length == 0)
        {
            _logger.LogInformation("Nothing to do.");

            return;
        }

        var settings = OrderManagementSettings.Default with
        {
            UnfilledOrderTimeout = TimeSpan.TryParse(_yoloConfig.UnfilledOrderTimeout, out var timeout)
                ? timeout
                : OrderManagementSettings.Default.UnfilledOrderTimeout
        };

        _logger.LogInformation("Managing orders for {TradeCount} trades", trades.Length);

        try
        {
            await foreach (var update in _broker.ManageOrdersAsync(trades, settings, cancellationToken))
            {
                if (update.Type == OrderUpdateType.Error)
                {
                    _logger.OrderError(update.Symbol, update.Message, 1);
                }
                else
                {
                    _logger.OrderUpdate(update.Symbol, update.Type, update.Message);
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
}
