using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using static YoloTrades.WellKnown;

namespace YoloTrades
{

    public static partial class LoggerExtensions
    {
        [LoggerMessage(
            EventId = TradeEventIds.CalculateTrades,
            Level = LogLevel.Debug, 
            Message = "CalculateTrades:\n\n{Weights}\n\n{Positions}\n\n{Markets}")]
        public static partial void CalculateTrades(
            this ILogger logger,
            IEnumerable<Weight> weights,
            IDictionary<string, Position> positions,
            IDictionary<string, IEnumerable<MarketInfo>> markets);

        [LoggerMessage(
            EventId = TradeEventIds.GeneratedTrade,
            Level = LogLevel.Information,
            Message = "({Token}): new {Trade} (current weight: {CurrentWeight:0.000}, target weight: {ConstrainedTargetWeight:0.000}, delta: {Delta:0.000})")]
        public static partial void GeneratedTrade(
            this ILogger logger,
            string token,
            decimal? currentWeight,
            decimal constrainedTargetWeight,
            decimal delta,
            Trade trade);

        [LoggerMessage(
            EventId = TradeEventIds.NoMarkets,
            Level = LogLevel.Error, 
            Message = "({Token}): no {AssetType} markets")]
        public static partial void NoMarkets(
            this ILogger logger,
            string token, 
            AssetType? assetType);

        [LoggerMessage(
            EventId = TradeEventIds.NoBid,
            Level = LogLevel.Error, 
            Message = "({Token}): no bid price for {Market}")]
        public static partial void NoBid(
            this ILogger logger,
            string token,
            string market);

        [LoggerMessage(
            EventId = TradeEventIds.NoAsk,
            Level = LogLevel.Error, 
            Message = "({Token}): no ask price for {Market}")]
        public static partial void NoAsk(
            this ILogger logger,
            string token,
            string market);

        [LoggerMessage(
            EventId = TradeEventIds.WithinTradeBuffer,
            Level = LogLevel.Information, 
            Message = "({Token}): no action - delta is within trade buffer (current weight: {CurrentWeight:0.000}, target weight: {ConstrainedTargetWeight:0.000}, delta: {Delta:0.000})")]
        public static partial void WithinTradeBuffer(
            this ILogger logger,
            string token,
            decimal currentWeight,
            decimal constrainedTargetWeight,
            decimal delta);
    }
}