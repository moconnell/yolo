using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;

namespace YoloTrades;

public class TradeFactory : ITradeFactory
{
    private readonly ILogger<TradeFactory> _logger;
    private readonly YoloConfig _yoloConfig;

    public TradeFactory(YoloConfig yoloConfig, ILogger<TradeFactory> logger)
    {
        _logger = logger;
        _yoloConfig = yoloConfig;
        AssetPermissions = yoloConfig.AssetPermissions;
        BaseAsset = yoloConfig.BaseAsset;
        MinOrderValue = yoloConfig.MinOrderValue;
        NominalCash = yoloConfig.NominalCash;
        SpreadSplit = Math.Max(0, Math.Min(1, yoloConfig.SpreadSplit));
    }

    private AssetPermissions AssetPermissions { get; }
    private string BaseAsset { get; }
    private decimal? MinOrderValue { get; }
    private decimal? NominalCash { get; }
    private decimal SpreadSplit { get; }

    public IEnumerable<Trade> CalculateTrades(
        IReadOnlyDictionary<string, decimal> weights,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets)
    {
        var nominal = NominalCash ??
                      positions.GetTotalValue(markets, BaseAsset);
        var plan = RebalancePlanner.Create(weights, positions, markets, _yoloConfig, nominal);
        var factorsDict = plan.Items.ToDictionary(
            item => item.Token,
            item => (Weight: item.RawTargetWeight, item.IsInUniverse));

        _logger.CalculateTrades(factorsDict, positions, markets);

        foreach (var item in plan.Items)
        {
            _logger.Weight(item.Token, item.RawTargetWeight);
            if (!item.HasTradableMarket)
                _logger.NoMarkets(item.Token);
            if (item.HasMultipleOpenPositions)
                _logger.MultiplePositions(item.Token);
            if (item.WithinTradeBuffer && item.CurrentWeight.HasValue)
            {
                _logger.WithinTradeBuffer(
                    item.Token,
                    item.CurrentWeight.Value,
                    item.ConstrainedTargetWeight,
                    item.ConstrainedTargetWeight - item.CurrentWeight.Value);
            }
        }

        return plan.Items
            .Select(RebalancePosition)
            .SelectMany(CombineOrders);

        IEnumerable<Trade> RebalancePosition(RebalancePlanItem item)
        {
            var rebalanceTarget = item.BufferAdjustedTargetWeight;
            if (rebalanceTarget is null)
                yield break;

            var remainingDelta = rebalanceTarget.Value - item.CurrentWeight.GetValueOrDefault();

            while (remainingDelta != 0)
            {
                var trades = CalcTrades(
                        item.Token,
                        [.. item.ProjectedPositions.Values],
                        rebalanceTarget.Value,
                        remainingDelta)
                    .ToArray();

                if (trades.Length == 0)
                    yield break;

                foreach (var (trade, remainingDeltaPostTrade) in trades)
                {
                    var currentProjectedPosition = item.ProjectedPositions[trade.Symbol];
                    var newProjectedPosition = currentProjectedPosition + trade;
                    item.ProjectedPositions[trade.Symbol] = newProjectedPosition;
                    // For tokens that dropped out of the universe, bypass MinOrderValue check
                    var minOrderValueForCheck = item.IsInUniverse ? MinOrderValue : null;
                    if (trade.IsTradable(minOrderValueForCheck))
                        yield return trade;
                    else
                        _logger.DeltaTooSmall(item.Token, remainingDelta);
                    remainingDelta = remainingDeltaPostTrade;
                }
            }
        }
    }

    private static IEnumerable<Trade> CombineOrders(IEnumerable<Trade> trades)
    {
        return trades.GroupBy(t => t.Symbol)
            .Select(g => g.Sum());
    }

    private IEnumerable<(Trade trade, decimal remainingDelta)> CalcTrades(
        string token,
        IReadOnlyList<ProjectedPosition> projectedPositions,
        decimal rebalanceTarget,
        decimal remainingDelta)
    {
        var startingWeight = rebalanceTarget - remainingDelta;
        var crossingZeroWeightBoundary = startingWeight != 0 && rebalanceTarget / startingWeight < 0;

        var marketPositions = projectedPositions.Count(HasPosition) switch
        {
            1 when !crossingZeroWeightBoundary => projectedPositions.Where(HasPosition).ToArray(),
            _ => [.. projectedPositions]
        };

        var isBuy = remainingDelta >= 0;
        var priceMultiplier = isBuy ? 1 : -1;

        var orderedMarkets = marketPositions
            .OrderByDescending(HasPosition)
            .ThenBy(projectedPosition => priceMultiplier * projectedPosition.Market.Last)
            .ToArray();

        _logger.MarketPositions(token, orderedMarkets);

        foreach (var projectedPosition in orderedMarkets)
        {
            var market = projectedPosition.Market;
            var nominal = projectedPosition.Nominal;
            var price = GetPrice(isBuy, market);

            if (price is null)
                continue;

            if (projectedPosition.ProjectedWeight is not { } weight)
                continue;

            var (delta, restart) = market.AssetType switch
            {
                AssetType.Future when market.Expiry is { } &&
                                      !AssetPermissions.HasFlag(AssetPermissions.ExpiringFutures) => (0, false),
                AssetType.Future when market.Expiry is null &&
                                      !AssetPermissions.HasFlag(AssetPermissions.PerpetualFutures) => (0, false),
                AssetType.Spot when weight + remainingDelta > 0 &&
                                    !AssetPermissions.HasFlag(AssetPermissions.LongSpot) => (-weight, false),
                AssetType.Spot when weight + remainingDelta < 0 &&
                                    !AssetPermissions.HasFlag(AssetPermissions.ShortSpot) => (-weight, false),
                _ when crossingZeroWeightBoundary => (-weight, true),
                _ => (remainingDelta, false)
            };

            if (delta == 0)
                continue;

            var rawSize = delta * nominal / price.Value;
            var reduceOnly = rebalanceTarget == 0 || projectedPosition.HasPosition && Math.Sign(weight) != Math.Sign(delta);
            var size = market.QuantityStep is null
                ? rawSize
                : reduceOnly
                    ? Math.Ceiling(Math.Abs(rawSize) / market.QuantityStep.Value) * market.QuantityStep.Value * Math.Sign(rawSize)
                    : Math.Floor(Math.Abs(rawSize) / market.QuantityStep.Value) * market.QuantityStep.Value * Math.Sign(rawSize);

            if (reduceOnly)
            {
                var projectedAmount = projectedPosition.ProjectedAmount;
                var maxReduceOnlySize = Math.Abs(projectedAmount);

                size = projectedAmount switch
                {
                    > 0 => Math.Max(size, -maxReduceOnlySize),
                    < 0 => Math.Min(size, maxReduceOnlySize),
                    _ => 0
                };
            }

            var trade = market.MinProvideSize switch
            {
                _ when Math.Abs(size) >= market.MinProvideSize.GetValueOrDefault() => new Trade(
                    market.Name,
                    market.AssetType,
                    size,
                    CalcLimitPrice(),
                    OrderType.Limit,
                    true,
                    reduceOnly),
                _ => new Trade(
                    market.Name,
                    market.AssetType,
                    size,
                    isBuy ? market.Ask!.Value : market.Bid!.Value,
                    OrderType.Market,
                    false,
                    reduceOnly)
            };

            if (trade.IsTradable())
                _logger.GeneratedTrade(token, delta, trade);

            remainingDelta -= delta;

            yield return (trade, remainingDelta);

            if (restart || remainingDelta == 0)
                break;
            continue;

            decimal? CalcLimitPrice()
            {
                var factor = isBuy ? SpreadSplit : 1 - SpreadSplit;
                var spread = market.Ask!.Value - market.Bid!.Value;
                var rawLimitPrice = market.Bid!.Value + spread * factor;
                var limitPriceSteps = rawLimitPrice / market.PriceStep;
                var limitPrice = limitPriceSteps is null
                    ? null
                    : (isBuy
                          ? Math.Floor(limitPriceSteps.Value)
                          : Math.Ceiling(limitPriceSteps.Value)) *
                      market.PriceStep;
                return limitPrice;
            }
        }

        yield break;

        bool HasPosition(ProjectedPosition p) => p.HasPosition;
    }

    private decimal? GetPrice(bool isBuy, MarketInfo market)
    {
        decimal? LogNull(decimal? value, Action<string, string> logFunc)
        {
            if (value is null)
                logFunc(market.BaseAsset, market.Name);

            return value;
        }

        return isBuy ? LogNull(market.Ask, _logger.NoAsk) : LogNull(market.Bid, _logger.NoBid);
    }

}
