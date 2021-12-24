using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using static YoloTrades.WellKnown;

namespace YoloTrades;

public static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = TradeEventIds.CalculateTrades,
        Level = LogLevel.Debug,
        Message = "*** CalculateTrades ***\nWeights: {Weights}\nPositions: {Positions}\nMarkets: {Markets}")]
    public static partial void CalculateTrades(
        this ILogger logger,
        Dictionary<string, (Weight weight, bool isInUniverse)> weights,
        IDictionary<string, IEnumerable<Position>> positions,
        IDictionary<string, IEnumerable<MarketInfo>> markets);

    [LoggerMessage(
        EventId = TradeEventIds.Weight,
        Level = LogLevel.Debug,
        Message = "({Token}): processing {Weight}")]
    public static partial void Weight(this ILogger logger, string token, Weight weight);

    [LoggerMessage(
        EventId = TradeEventIds.MarketPositions,
        Level = LogLevel.Debug,
        Message = "({Token}): market positions... {MarketPositions}")]
    public static partial void MarketPositions(
        this ILogger logger,
        string token,
        IEnumerable<ProjectedPosition> marketPositions);

    [LoggerMessage(
        EventId = TradeEventIds.GeneratedTrade,
        Level = LogLevel.Debug,
        Message =
            "({Token}): new {Trade} (delta: {Delta:0.000})")]
    public static partial void GeneratedTrade(
        this ILogger logger,
        string token,
        decimal delta,
        Trade trade);

    [LoggerMessage(
        EventId = TradeEventIds.NoMarkets,
        Level = LogLevel.Error,
        Message = "({Token}): no markets")]
    public static partial void NoMarkets(
        this ILogger logger,
        string token);

    [LoggerMessage(
        EventId = TradeEventIds.MultiplePositions,
        Level = LogLevel.Error,
        Message = "({Token}): multiple positions!")]
    public static partial void MultiplePositions(
        this ILogger logger,
        string token);

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
        Level = LogLevel.Debug,
        Message =
            "({Token}): no action - delta is within trade buffer (current weight: {CurrentWeight:0.000}, target weight: {ConstrainedTargetWeight:0.000}, delta: {Delta:0.000})")]
    public static partial void WithinTradeBuffer(
        this ILogger logger,
        string token,
        decimal currentWeight,
        decimal constrainedTargetWeight,
        decimal delta);

    [LoggerMessage(
        EventId = TradeEventIds.DeltaTooSmall,
        Level = LogLevel.Debug,
        Message =
            "({Token}): no action - delta too small to trade (delta: {Delta:0.0000})")]
    public static partial void DeltaTooSmall(
        this ILogger logger,
        string token,
        decimal delta);
}