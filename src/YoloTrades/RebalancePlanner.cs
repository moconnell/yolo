using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;

namespace YoloTrades;

public static class RebalancePlanner
{
    public static RebalancePlan Create(
        IReadOnlyDictionary<string, decimal> weights,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets,
        YoloConfig config,
        decimal nominal)
    {
        var positionsByToken = ToCaseInsensitiveDictionary(positions);
        var marketsByToken = ToCaseInsensitiveDictionary(markets);
        var factors = weights
            .GroupBy(kvp => kvp.Key.GetBaseAndQuoteAssets().BaseAsset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (Weight: group.Sum(x => x.Value), IsInUniverse: true),
                StringComparer.OrdinalIgnoreCase);

        var unconstrainedTargetLeverage = factors
            .Values
            .Sum(w => Math.Abs(w.Weight));

        var weightConstraint = unconstrainedTargetLeverage < config.MaxLeverage
            ? 1
            : config.MaxLeverage / unconstrainedTargetLeverage;

        var droppedTokens = positionsByToken.Keys.Except(
            factors.Keys.Append(config.BaseAsset),
            StringComparer.OrdinalIgnoreCase);
        foreach (var token in droppedTokens)
        {
            factors[token] = (0m, false);
        }

        var items = factors
            .Select(kvp => CreateItem(kvp, positionsByToken, marketsByToken, config, nominal, weightConstraint))
            .ToArray();

        SetBufferAdjustedTargets(items, config.RebalanceMode, config.TradeBuffer);

        return new RebalancePlan(weightConstraint, items);
    }

    private static Dictionary<string, IReadOnlyList<T>> ToCaseInsensitiveDictionary<T>(
        IReadOnlyDictionary<string, IReadOnlyList<T>> values) =>
        values
            .GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<T> (group) => group.SelectMany(kvp => kvp.Value).ToArray(),
                StringComparer.OrdinalIgnoreCase);

    private static RebalancePlanItem CreateItem(
        KeyValuePair<string, (decimal Weight, bool IsInUniverse)> kvp,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets,
        YoloConfig config,
        decimal nominal,
        decimal weightConstraint)
    {
        var (token, (weight, isInUniverse)) = kvp;
        var tokenPositions = positions.TryGetValue(token, out var pos)
            ? pos.ToArray()
            : [];

        var constrainedTargetWeight = weightConstraint * weight;
        var projectedPositions = markets.GetMarkets(token)
            .ToDictionary(
                market => market.Name,
                market =>
                {
                    var position = tokenPositions
                                       .FirstOrDefault(p =>
                                           p.AssetType == market.AssetType &&
                                           (string.Equals(p.AssetName, market.Name, StringComparison.OrdinalIgnoreCase) &&
                                            market.AssetType == AssetType.Future ||
                                            string.Equals(p.BaseAsset, market.BaseAsset, StringComparison.OrdinalIgnoreCase) &&
                                            market.AssetType == AssetType.Spot)) ??
                                   Position.Null;

                    return new ProjectedPosition(market, position.Amount, nominal);
                },
                StringComparer.OrdinalIgnoreCase);

        var hasTradableMarket = projectedPositions.Count != 0;
        var hasMultipleOpenPositions = projectedPositions.Count(kvp => kvp.Value.HasPosition) > 1;
        var currentWeight = hasTradableMarket
            ? projectedPositions.Values.Sum(projectedPosition => projectedPosition.ProjectedWeight)
            : null;

        var withinTradeBuffer = isInUniverse &&
                                currentWeight.HasValue &&
                                Math.Abs(currentWeight.Value - constrainedTargetWeight) <= config.TradeBuffer;

        return new RebalancePlanItem(
            token,
            weight,
            isInUniverse,
            constrainedTargetWeight,
            currentWeight,
            withinTradeBuffer,
            hasTradableMarket,
            hasMultipleOpenPositions,
            projectedPositions);
    }

    private static void SetBufferAdjustedTargets(
        IReadOnlyList<RebalancePlanItem> items,
        RebalanceMode rebalanceMode,
        decimal tradeBuffer)
    {
        foreach (var item in items)
        {
            item.BufferAdjustedTargetWeight = item switch
            {
                { CurrentWeight: null } => null,
                { WithinTradeBuffer: true } => item.CurrentWeight,
                { IsInUniverse: false } => 0m,
                _ when rebalanceMode == RebalanceMode.Edge => CalculateEdgeTarget(
                    item.CurrentWeight.GetValueOrDefault(),
                    item.ConstrainedTargetWeight,
                    tradeBuffer),
                _ => item.ConstrainedTargetWeight
            };
        }

        if (rebalanceMode != RebalanceMode.Edge)
            return;

        NormalizeSide(
            items,
            targetWeight => targetWeight > 0,
            currentWeight => currentWeight > 0,
            weight => weight);
        NormalizeSide(
            items,
            targetWeight => targetWeight < 0,
            currentWeight => currentWeight < 0,
            Math.Abs);
    }

    private static void NormalizeSide(
        IReadOnlyList<RebalancePlanItem> items,
        Func<decimal, bool> isTargetSide,
        Func<decimal, bool> isCurrentSide,
        Func<decimal, decimal> getExposure)
    {
        var targetExposure = items
            .Where(item => isTargetSide(item.ConstrainedTargetWeight))
            .Sum(item => getExposure(item.ConstrainedTargetWeight));

        var fixedExposure = items
            .Where(item => item.WithinTradeBuffer &&
                           item.CurrentWeight.HasValue &&
                           isCurrentSide(item.CurrentWeight.Value))
            .Sum(item => getExposure(item.CurrentWeight.GetValueOrDefault()));

        var adjustableItems = items
            .Where(item => !item.WithinTradeBuffer &&
                           item.BufferAdjustedTargetWeight is { } target &&
                           isTargetSide(target))
            .ToArray();

        var adjustableExposure = adjustableItems
            .Sum(item => getExposure(item.BufferAdjustedTargetWeight.GetValueOrDefault()));

        var remainingExposure = targetExposure - fixedExposure;
        if (remainingExposure <= 0 || adjustableExposure == 0)
            return;

        var scale = remainingExposure / adjustableExposure;
        foreach (var item in adjustableItems)
        {
            item.BufferAdjustedTargetWeight *= scale;
        }
    }

    private static decimal CalculateEdgeTarget(decimal currentWeight, decimal idealWeight, decimal tradeBuffer) =>
        currentWeight > idealWeight
            ? idealWeight + tradeBuffer
            : idealWeight - tradeBuffer;
}

public sealed record RebalancePlan(
    decimal WeightConstraint,
    IReadOnlyList<RebalancePlanItem> Items);

public sealed class RebalancePlanItem(
    string token,
    decimal rawTargetWeight,
    bool isInUniverse,
    decimal constrainedTargetWeight,
    decimal? currentWeight,
    bool withinTradeBuffer,
    bool hasTradableMarket,
    bool hasMultipleOpenPositions,
    Dictionary<string, ProjectedPosition> projectedPositions)
{
    public string Token { get; } = token;
    public decimal RawTargetWeight { get; } = rawTargetWeight;
    public bool IsInUniverse { get; } = isInUniverse;
    public decimal ConstrainedTargetWeight { get; } = constrainedTargetWeight;
    public decimal? CurrentWeight { get; } = currentWeight;
    public bool WithinTradeBuffer { get; } = withinTradeBuffer;
    public bool HasTradableMarket { get; } = hasTradableMarket;
    public bool HasMultipleOpenPositions { get; } = hasMultipleOpenPositions;
    public Dictionary<string, ProjectedPosition> ProjectedPositions { get; } = projectedPositions;
    public decimal? BufferAdjustedTargetWeight { get; set; }
    public decimal? DeltaWeight => BufferAdjustedTargetWeight.HasValue && CurrentWeight.HasValue
        ? BufferAdjustedTargetWeight.Value - CurrentWeight.Value
        : null;
}
