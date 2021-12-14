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

        var trades = new List<Trade>();

        void AddTrade(Weight w)
        {
            var (token, _) = w.Ticker.SplitConstituents();

            var tokenPositions = positions.TryGetValue(token, out var pos)
                ? pos.ToArray()
                : Array.Empty<Position>();
            var constrainedTargetWeight = weightConstraint * w.ComboWeight;

            _logger.LogDebug("Processing weight: {Weight}", w);

            var marketPositions = markets.GetMarkets(token)
                .Select(marketInfo =>
                {
                    var currentPosition = tokenPositions
                        .FirstOrDefault(p => p.AssetName == marketInfo.Name) ?? Position.Null;

                    return (marketInfo, currentPosition);
                })
                .ToArray();

            if (!marketPositions.Any())
            {
                _logger.NoMarkets(token);
                return;
            }

            var currentWeight = marketPositions.Sum(tuple =>
            {
                var (marketInfo, currentPosition) = tuple;
                if (marketInfo.Bid is null)
                    _logger.NoBid(token, marketInfo.Key);
                
                return currentPosition.Amount * marketInfo.Bid / nominal;
            });

            if (currentWeight is null)
            {
                return;
            }

            if (RebalanceMode == RebalanceMode.Slow &&
                currentWeight >= constrainedTargetWeight - TradeBuffer &&
                currentWeight <= constrainedTargetWeight + TradeBuffer)
            {
                _logger.WithinTradeBuffer(
                    token,
                    currentWeight.Value,
                    constrainedTargetWeight,
                    constrainedTargetWeight - currentWeight.Value);
                return;
            }

            var tradeBufferAdjustment = TradeBuffer * RebalanceMode switch
            {
                RebalanceMode.Slow => constrainedTargetWeight < currentWeight ? 1 : -1,
                _ => 0
            };
            var remainingDelta = constrainedTargetWeight - currentWeight.Value + tradeBufferAdjustment;
            var isBuy = remainingDelta > 0;
            var orderedMarkets = isBuy
                ? marketPositions.OrderBy(tuple => tuple.marketInfo.Last).ToArray()
                : marketPositions.OrderByDescending(tuple => tuple.marketInfo.Last).ToArray();
            var i = 0;
            
            while (remainingDelta != 0 && i < orderedMarkets.Length)
            {
                var (market, currentPosition) = orderedMarkets[i++];
                var currentMarketWeight = currentPosition.Amount * market.Bid / nominal;
                var price = GetPrice(isBuy, market);

                if (price is null)
                    return;

                var delta = market.AssetType switch
                {
                    AssetType.Future when market.Expiry is {} && !AssetPermissions.HasFlag(AssetPermissions.ExpiringFutures) => 0,
                    AssetType.Future when market.Expiry is null && !AssetPermissions.HasFlag(AssetPermissions.PerpetualFutures) => 0,
                    AssetType.Spot when currentMarketWeight + remainingDelta > 0 && !AssetPermissions.HasFlag(AssetPermissions.LongSpot) => -currentMarketWeight!.Value,
                    AssetType.Spot when currentMarketWeight + remainingDelta < 0 && !AssetPermissions.HasFlag(AssetPermissions.ShortSpot) => -currentMarketWeight!.Value,
                    _ => remainingDelta
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

                _logger.GeneratedTrade(
                    token,
                    currentWeight,
                    constrainedTargetWeight,
                    delta,
                    trade);

                trades.Add(trade);

                remainingDelta -= delta;
            }
        }

        weightsList
            .ForEach(AddTrade);

        return trades;
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