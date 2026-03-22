using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;
using YoloBroker.Interface;
using YoloFunk.Dto;
using YoloTrades;

namespace YoloFunk.Functions;

public abstract class EffectiveWeightsFunctionBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    protected EffectiveWeightsFunctionBase(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected abstract string StrategyKey { get; }

    protected async Task<HttpResponseData> GetEffectiveWeightsAsync(HttpRequestData req, CancellationToken cancellationToken)
    {
        try
        {
            var broker = _serviceProvider.GetRequiredKeyedService<IYoloBroker>(StrategyKey);
            var accountContext = broker.GetAccountContext();
            var address = accountContext.Address;
            var vaultAddress = accountContext.VaultAddress;

            if (string.IsNullOrWhiteSpace(address))
            {
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(
                    new RebalanceErrorResponse(
                        StrategyKey,
                        "Invalid strategy configuration",
                        "Broker account context is missing address."),
                    cancellationToken);
                return error;
            }

            var weightsService = _serviceProvider.GetRequiredKeyedService<ICalcWeights>(StrategyKey);
            var yoloConfig = _serviceProvider.GetRequiredKeyedService<YoloConfig>(StrategyKey);
            var targetWeights = await weightsService.CalculateWeightsAsync(cancellationToken);

            var positions = await broker.GetPositionsAsync(cancellationToken);
            var baseAssetFilter = positions.Keys
                .Union(targetWeights.Keys.Select(x => x.GetBaseAndQuoteAssets().BaseAsset))
                .ToHashSet();

            var markets = await broker.GetMarketsAsync(
                baseAssetFilter,
                yoloConfig.BaseAsset,
                yoloConfig.AssetPermissions,
                cancellationToken);

            var nominal = yoloConfig.NominalCash ?? positions.GetTotalValue(markets, yoloConfig.BaseAsset);
            var verification = CalculateEffectiveWeights(targetWeights, positions, markets, yoloConfig, nominal);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(
                new EffectiveWeightsResponse(
                    StrategyKey,
                    address,
                    vaultAddress,
                    DateTime.UtcNow,
                    nominal,
                    verification.WeightConstraint,
                    verification.Weights),
                cancellationToken);
            return response;
        }
        catch (DuplicateBaseAssetWeightsException ex)
        {
            _logger.LogWarning(ex,
                "Invalid raw weights for strategy {Strategy}",
                StrategyKey);

            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "Invalid raw weights",
                    ex.Message),
                cancellationToken);
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to calculate effective weights for strategy {Strategy}",
                StrategyKey);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(
                new RebalanceErrorResponse(
                    StrategyKey,
                    "Failed to calculate effective weights",
                    "An internal error occurred. Check logs for details."),
                cancellationToken);
            return errorResponse;
        }
    }

    private static (decimal WeightConstraint, IReadOnlyList<EffectiveWeightItem> Weights) CalculateEffectiveWeights(
        IReadOnlyDictionary<string, decimal> rawWeights,
        IReadOnlyDictionary<string, IReadOnlyList<Position>> positions,
        IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> markets,
        YoloConfig yoloConfig,
        decimal nominal)
    {
        var groupedWeights = rawWeights
            .GroupBy(kvp => kvp.Key.GetBaseAndQuoteAssets().BaseAsset, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicateGroups = groupedWeights
            .Where(group => group.Count() > 1)
            .ToArray();

        if (duplicateGroups.Length > 0)
        {
            var duplicatesDescription = string.Join(
                "; ",
                duplicateGroups.Select(group =>
                    $"{group.Key}: {string.Join(", ", group.Select(x => x.Key).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}"));

            throw new DuplicateBaseAssetWeightsException(
                $"Duplicate raw weight symbols normalize to the same base asset: {duplicatesDescription}");
        }

        var factors = groupedWeights.ToDictionary(
            group => group.Key,
            group => (Weight: group.Sum(x => x.Value), IsInUniverse: true),
            StringComparer.OrdinalIgnoreCase);

        var unconstrainedTargetLeverage = factors
            .Values
            .Sum(w => Math.Abs(w.Weight));

        var weightConstraint = unconstrainedTargetLeverage < yoloConfig.MaxLeverage
            ? 1
            : yoloConfig.MaxLeverage / unconstrainedTargetLeverage;

        var droppedTokens = positions.Keys.Except(factors.Keys.Union([yoloConfig.BaseAsset]));
        foreach (var token in droppedTokens)
        {
            factors[token] = (0m, false);
        }

        var effectiveItems = new List<EffectiveWeightItem>(factors.Count);

        foreach (var (token, (weight, isInUniverse)) in factors.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var tokenPositions = positions.TryGetValue(token, out var pos)
                ? pos.ToArray()
                : [];

            var constrainedTargetWeight = weightConstraint * weight;
            var marketList = markets.GetMarkets(token);
            var projectedPositions = marketList
                .ToDictionary(
                    market => market.Name,
                    market =>
                    {
                        var position = tokenPositions
                                           .FirstOrDefault(p =>
                                               p.AssetType == market.AssetType &&
                                               (p.AssetName == market.Name && market.AssetType == AssetType.Future ||
                                                p.BaseAsset == market.BaseAsset && market.AssetType == AssetType.Spot)) ??
                                       Position.Null;

                        return new ProjectedPosition(market, position.Amount, nominal);
                    });

            var hasTradableMarket = projectedPositions.Count != 0;
            var hasMultipleOpenPositions = projectedPositions.Count(kvp => kvp.Value.HasPosition) > 1;
            var currentWeight = hasTradableMarket && !hasMultipleOpenPositions
                ? projectedPositions.Values.Sum(projectedPosition => projectedPosition.ProjectedWeight)
                : null;

            var withinTradeBuffer = isInUniverse &&
                                    currentWeight.HasValue &&
                                    Math.Abs(currentWeight.Value - constrainedTargetWeight) <= yoloConfig.TradeBuffer;

            decimal? effectiveWeight = currentWeight.HasValue
                ? withinTradeBuffer
                    ? currentWeight.Value
                    : yoloConfig.RebalanceMode switch
                    {
                        RebalanceMode.Edge => CalculateEdgeTarget(currentWeight.Value, constrainedTargetWeight, yoloConfig.TradeBuffer),
                        _ => constrainedTargetWeight
                    }
                : null;

            decimal? deltaWeight = effectiveWeight.HasValue && currentWeight.HasValue
                ? effectiveWeight.Value - currentWeight.Value
                : null;

            effectiveItems.Add(new EffectiveWeightItem(
                token,
                weight,
                constrainedTargetWeight,
                currentWeight,
                effectiveWeight,
                deltaWeight,
                isInUniverse,
                withinTradeBuffer,
                hasTradableMarket));
        }

        return (weightConstraint, effectiveItems);
    }

    private static decimal CalculateEdgeTarget(decimal currentWeight, decimal idealWeight, decimal tradeBuffer) =>
        currentWeight > idealWeight
            ? idealWeight + tradeBuffer
            : idealWeight - tradeBuffer;

    private sealed class DuplicateBaseAssetWeightsException(string message) : InvalidOperationException(message);

}