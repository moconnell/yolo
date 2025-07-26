using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;

namespace YoloTrades;

public class TradeFactory : ITradeFactory
{
    private readonly ILogger<TradeFactory> _logger;

    public TradeFactory(ILogger<TradeFactory> logger, IConfiguration configuration)
        : this(
            logger,
            configuration.GetYoloConfig()
                ?? throw new InvalidOperationException("YoloConfig is required but was not found in configuration")
        )
    {
    }

    public TradeFactory(ILogger<TradeFactory> logger, YoloConfig yoloConfig)
    {
        _logger = logger;
        AssetPermissions = yoloConfig.AssetPermissions;
        TradeBuffer = yoloConfig.TradeBuffer;
        MaxLeverage = yoloConfig.MaxLeverage;
        NominalCash = yoloConfig.NominalCash;
        BaseCurrencyToken = yoloConfig.BaseAsset;
        SpreadSplit = Math.Max(0, Math.Min(1, yoloConfig.SpreadSplit));
    }

    private AssetPermissions AssetPermissions { get; }
    private string BaseCurrencyToken { get; }
    private decimal? NominalCash { get; }
    private decimal MaxLeverage { get; }
    private decimal TradeBuffer { get; }
    private decimal SpreadSplit { get; }

    public IEnumerable<Trade> CalculateTrades(
        IReadOnlyList<Weight> weights,
        IDictionary<string, IReadOnlyList<Position>> positions,
        IDictionary<string, IReadOnlyList<MarketInfo>> markets)
    {
        var nominal = NominalCash ??
                      positions.GetTotalValue(markets, BaseCurrencyToken);
        var weightsDict = weights.ToDictionary(
            weight => weight.Ticker.GetBaseAndQuoteAssets().BaseAsset,
            weight => (weight, isInUniverse: true));

        _logger.CalculateTrades(weightsDict, positions, markets);

        var unconstrainedTargetLeverage = weightsDict
            .Values
            .Select(w => Math.Abs(w.weight.ComboWeight))
            .Sum();

        var weightConstraint = unconstrainedTargetLeverage < MaxLeverage
            ? 1
            : MaxLeverage / unconstrainedTargetLeverage;

        var droppedTokens = positions.Keys.Except(weightsDict.Keys.Union([BaseCurrencyToken]));
        foreach (var token in droppedTokens)
        {
            var weight = new Weight(0, 0, DateTime.UtcNow, 0, $"{token}/{BaseCurrencyToken}", 0);
            weightsDict[token] = (weight, false);
        }

        return weightsDict
            .Select(RebalancePosition)
            .SelectMany(CombineOrders);

        IEnumerable<Trade> RebalancePosition(KeyValuePair<string, (Weight, bool)> kvp)
        {
            var (token, (w, isInUniverse)) = kvp;

            _logger.Weight(token, w);

            var tokenPositions = positions.TryGetValue(token, out var pos)
                ? pos.ToArray()
                : [];
            var constrainedTargetWeight = weightConstraint * w.ComboWeight;

            var projectedPositions = markets.GetMarkets(token)
                .ToDictionary(
                    market => market.Name,
                    market =>
                    {
                        if (market.Bid is null)
                            _logger.NoBid(token, market.Key);
                        if (market.Ask is null)
                            _logger.NoAsk(token, market.Key);

                        var position = tokenPositions
                                           .FirstOrDefault(p =>
                                               p.AssetType == market.AssetType &&
                                               (p.AssetName == market.Name &&
                                                market.AssetType == AssetType.Future ||
                                                p.BaseAsset == market.BaseAsset &&
                                                market.AssetType == AssetType.Spot)) ??
                                       Position.Null;

                        return new ProjectedPosition(market, position.Amount, nominal);
                    });

            if (projectedPositions.Count == 0)
            {
                _logger.NoMarkets(token);
                yield break;
            }

            bool HasPosition(KeyValuePair<string, ProjectedPosition> keyValuePair) => keyValuePair.Value.HasPosition;

            if (projectedPositions.Count(HasPosition) > 1)
            {
                _logger.MultiplePositions(token);
                yield break;
            }

            var currentWeight = projectedPositions.Values.Sum(projectedPosition => projectedPosition.ProjectedWeight);
            if (currentWeight is null)
                yield break;

            if (isInUniverse &&
                currentWeight >= constrainedTargetWeight - TradeBuffer &&
                currentWeight <= constrainedTargetWeight + TradeBuffer)
            {
                _logger.WithinTradeBuffer(
                    token,
                    currentWeight.Value,
                    constrainedTargetWeight,
                    constrainedTargetWeight - currentWeight.Value);
                yield break;
            }

            var tradeBufferAdjustment =
                isInUniverse ? TradeBuffer * (constrainedTargetWeight < currentWeight ? 1 : -1) : 0;
            var bufferedTargetWeight = constrainedTargetWeight + tradeBufferAdjustment;
            var remainingDelta = bufferedTargetWeight - currentWeight.Value;

            while (remainingDelta != 0)
            {
                var trades = CalcTrades(
                    token,
                    [.. projectedPositions.Values],
                    bufferedTargetWeight,
                    remainingDelta);

                foreach (var (trade, remainingDeltaPostTrade) in trades)
                {
                    var currentProjectedPosition = projectedPositions[trade.AssetName];
                    var newProjectedPosition = currentProjectedPosition + trade;
                    projectedPositions[trade.AssetName] = newProjectedPosition;
                    if (trade.IsTradable)
                        yield return trade;
                    else
                        _logger.DeltaTooSmall(token, remainingDelta);
                    remainingDelta = remainingDeltaPostTrade;
                }
            }
        }
    }

    private static IEnumerable<Trade> CombineOrders(IEnumerable<Trade> trades)
    {
        return trades.GroupBy(t => t.AssetName)
            .Select(g => g.Sum());
    }

    private IEnumerable<(Trade trade, decimal remainingDelta)> CalcTrades(string token,
        IReadOnlyList<ProjectedPosition> projectedPositions,
        decimal bufferedTargetWeight,
        decimal remainingDelta)
    {
        var startingWeight = bufferedTargetWeight - remainingDelta;
        var crossingZeroWeightBoundary = startingWeight != 0 && bufferedTargetWeight / startingWeight < 0;

        var marketPositions = projectedPositions.Count(HasPosition) switch
        {
            1 when !crossingZeroWeightBoundary => projectedPositions.Where(HasPosition).ToArray(),
            _ => projectedPositions.ToArray()
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
            var weight = projectedPosition.ProjectedWeight!.Value;
            var nominal = projectedPosition.Nominal;
            var price = GetPrice(isBuy, market);

            if (price is null)
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
            var size = market.QuantityStep is null
                ? rawSize
                : Math.Floor(rawSize / market.QuantityStep.Value) * market.QuantityStep.Value;

            var trade = market.MinProvideSize switch
            {
                _ when Math.Abs(size) >= market.MinProvideSize => new Trade(market.Name, market.AssetType, size, CalcLimitPrice(), true),
                _ => new Trade(market.Name, market.AssetType, size, isBuy ? market.Ask!.Value : market.Bid!.Value, false)
            };

            if (trade.IsTradable)
                _logger.GeneratedTrade(token, delta, trade);

            remainingDelta -= delta;

            yield return (trade, remainingDelta);

            if (restart || remainingDelta == 0) break;
            continue;

            decimal? CalcLimitPrice()
            {
                var factor = isBuy ? SpreadSplit : 1 - SpreadSplit;
                var spread = market.Ask!.Value - market.Bid!.Value;
                var rawLimitPrice = market.Bid!.Value + spread * factor;
                var limitPriceSteps = rawLimitPrice / market.PriceStep;
                return limitPriceSteps is null
                    ? null
                    : (isBuy
                          ? Math.Floor(limitPriceSteps.Value)
                          : Math.Ceiling(limitPriceSteps.Value)) *
                      market.PriceStep;
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