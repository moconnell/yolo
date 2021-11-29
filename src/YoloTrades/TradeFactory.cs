﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloAbstractions.Config;

namespace YoloTrades
{
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
            TradeBuffer = yoloConfig.TradeBuffer;
            MaxLeverage = yoloConfig.MaxLeverage;
            NominalCash = yoloConfig.NominalCash;
            BaseCurrencyToken = yoloConfig.BaseAsset;
            AssetPermissions = yoloConfig.AssetPermissions;
        }

        private AssetPermissions AssetPermissions { get; }
        private string BaseCurrencyToken { get; }
        private decimal? NominalCash { get; }
        private decimal MaxLeverage { get; }
        private decimal TradeBuffer { get; }

        public IEnumerable<Trade> CalculateTrades(
            IEnumerable<Weight> weights,
            IDictionary<string, Position> positions,
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

            void Process(Weight w)
            {
                var (token, baseCurrency) = w.Ticker.SplitConstituents();

                // var baseCurrencyHoldings = positions
                //     .TryGetValue(baseCurrencyToken, out var pos)
                //     ? (baseCurrencyToken, baseCurrencyTokenWeight: pos.Amount)
                //     : (baseCurrencyToken, 0);

                var position = positions.TryGetValue(token, out var pos)
                    ? pos
                    : Position.Null;
                var constrainedTargetWeight = weightConstraint * w.ComboWeight;

                _logger.LogDebug("Processing weight: {Weight}", w);

                var tokenMarkets = markets.GetMarkets(
                        token)
                    .OrderBy(p => p.Last)
                    .Select(p => (price: p, currentWeight: position.Amount * p.Bid / nominal))
                    .ToArray();

                if (!tokenMarkets.Any())
                {
                    _logger.NoMarkets(token);
                    return;
                }

                var (market, currentWeight) =
                    constrainedTargetWeight - tokenMarkets.Last()
                        .currentWeight < 0
                        ? tokenMarkets.Last()
                        : tokenMarkets.First();

                var delta = constrainedTargetWeight - currentWeight!.Value;

                if (Math.Abs(delta) <= TradeBuffer)
                {
                    _logger.WithinTradeBuffer(
                        token,
                        currentWeight.Value,
                        constrainedTargetWeight,
                        delta);
                    return;
                }

                var isBuy = constrainedTargetWeight > currentWeight!.Value;

                if (constrainedTargetWeight < 0 && !isBuy && market.AssetType == AssetType.Spot &&
                    !AssetPermissions.HasFlag(AssetPermissions.ShortSpot))
                {
                    if (currentWeight > 0)
                    {
                        // close spot
                        var trade = CreateTrade(
                            market,
                            currentWeight.Value,
                            0,
                            nominal);

                        trades.Add(trade);
                    }
                    
                    // short the rest with futures
                }
                else
                {
                    // trade normally
                    var trade = CreateTrade(
                        market,
                        currentWeight.Value,
                        constrainedTargetWeight,
                        nominal);

                    trades.Add(trade);
                }

            }

            weightsList
                .ForEach(Process);

            return trades;
        }

        private Trade CreateTrade(MarketInfo market, decimal currentWeight, decimal targetWeight, decimal nominal)
        {
            var isBuy = targetWeight > currentWeight;
            var price = GetPrice(isBuy, market);
            if (price is null)
            {
                throw new TradeException("Missing price");
            }

            var delta = targetWeight - currentWeight;
            var rawSize = delta * nominal / price.Value;

            var size = Math.Floor(rawSize / market.QuantityStep) * market.QuantityStep;
            var factor = isBuy ? 0.618m : 0.382m;
            var spread = market.Ask!.Value - market.Bid!.Value;
            var rawLimitPrice = market.Bid!.Value + spread * factor;
            var limitPrice = Math.Floor(rawLimitPrice / market.PriceStep) * market.PriceStep;
            var trade = new Trade(market.Name, market.AssetType, size, limitPrice);

            _logger.GeneratedTrade(
                market.BaseAsset,
                currentWeight,
                targetWeight,
                delta,
                trade);

            return trade;
        }

        private decimal? GetPrice(bool isBuy, MarketInfo market)
        {
            decimal? LogNull(decimal? value, Action<string, string> logFunc)
            {
                if (value is null)
                {
                    logFunc(market.BaseAsset, market.Name);
                }

                return value;
            }

            return isBuy ? LogNull(market.Ask, _logger.NoAsk) : LogNull(market.Bid, _logger.NoBid);
        }
    }
}