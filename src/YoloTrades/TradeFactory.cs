using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloAbstractions.Config;

namespace YoloTrades;

public class TradeFactory : ITradeFactory
{
    private readonly ILogger<TradeFactory> _logger;

    public TradeFactory(ILogger<TradeFactory> logger, IConfiguration configuration)
        : this(logger, configuration.GetYoloConfig())
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
        RebalanceMode = yoloConfig.RebalanceMode;
        SpreadSplit = Math.Max(0, Math.Min(1, yoloConfig.SpreadSplit));
    }

    private AssetPermissions AssetPermissions { get; }
    private string BaseCurrencyToken { get; }
    private decimal? NominalCash { get; }
    private decimal MaxLeverage { get; }
    private decimal TradeBuffer { get; }
    private RebalanceMode RebalanceMode { get; }
    private decimal SpreadSplit { get; }

    public IEnumerable<Trade> CalculateTrades(
        IEnumerable<Weight> weights,
        IDictionary<string, IEnumerable<Position>> positions,
        IDictionary<string, IEnumerable<MarketInfo>> markets)
    {
        var nominal = NominalCash ??
                      positions.GetTotalValue(markets, BaseCurrencyToken);
        var weightsList = weights.ToList();

        _logger.CalculateTrades(weightsList, positions, markets);

        var unconstrainedTargetLeverage = weightsList
            .Select(w => Math.Abs(w.ComboWeight))
            .Sum();

        var weightConstraint = unconstrainedTargetLeverage < MaxLeverage
            ? 1
            : MaxLeverage / unconstrainedTargetLeverage;

        IEnumerable<Trade> RebalancePositions(Weight w)
        {
            var (token, _) = w.Ticker.SplitConstituents();

            var tokenPositions = positions.TryGetValue(token, out var pos)
                ? pos.ToArray()
                : Array.Empty<Position>();
            var constrainedTargetWeight = weightConstraint * w.ComboWeight;

            _logger.LogDebug("Processing weight: {Weight}", w);

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

            if (!projectedPositions.Any())
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

            if (RebalanceMode == RebalanceMode.Slow &&
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

            var tradeBufferAdjustment = TradeBuffer * RebalanceMode switch
            {
                RebalanceMode.Slow => constrainedTargetWeight < currentWeight ? 1 : -1,
                _ => 0
            };

            var bufferedTargetWeight = constrainedTargetWeight + tradeBufferAdjustment;
            var remainingDelta = bufferedTargetWeight - currentWeight.Value;

            while (remainingDelta != 0)
            {
                var trades = CalcTrades(token, projectedPositions.Values.ToArray(), bufferedTargetWeight, remainingDelta);
                
                foreach (var (trade, remainingDeltaPostTrade) in trades)
                {
                    var currentProjectedPosition = projectedPositions[trade.AssetName];
                    var newProjectedPosition = currentProjectedPosition + trade;
                    projectedPositions[trade.AssetName] = newProjectedPosition;
                    yield return trade;
                    remainingDelta = remainingDeltaPostTrade;
                }
            }
        }

        return weightsList
            .SelectMany(RebalancePositions);
    }

    private IEnumerable<(Trade trade, decimal remainingDelta)> CalcTrades(string token,
        IReadOnlyList<ProjectedPosition> projectedPositions,
        decimal bufferedTargetWeight,
        decimal remainingDelta)
    {
        bool HasPosition(ProjectedPosition p) => p.HasPosition;

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
            var size = Math.Floor(rawSize / market.QuantityStep) * market.QuantityStep;
            var factor = isBuy ? SpreadSplit : 1 - SpreadSplit;
            var spread = market.Ask!.Value - market.Bid!.Value;
            var rawLimitPrice = market.Bid!.Value + spread * factor;
            var limitPrice = Math.Floor(rawLimitPrice / market.PriceStep) * market.PriceStep;
            var trade = new Trade(market.Name, market.AssetType, size, limitPrice);

            _logger.GeneratedTrade(token, delta, trade);

            remainingDelta -= delta;
            
            yield return (trade, remainingDelta);
            
            if (restart)
            {
                break;
            }
        }
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