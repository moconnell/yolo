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
                    verification.CurrentGrossExposure,
                    verification.CurrentNetExposure,
                    verification.EffectiveGrossExposure,
                    verification.EffectiveNetExposure,
                    verification.BufferAdjustedGrossExposure,
                    verification.BufferAdjustedNetExposure,
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

    private static EffectiveWeightsVerification CalculateEffectiveWeights(
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

        var plan = RebalancePlanner.Create(rawWeights, positions, markets, yoloConfig, nominal);
        var effectiveItems = plan.Items
            .OrderBy(item => item.Token, StringComparer.OrdinalIgnoreCase)
            .Select(item => new EffectiveWeightItem(
                item.Token,
                item.RawTargetWeight,
                item.ConstrainedTargetWeight,
                item.CurrentWeight,
                item.BufferAdjustedTargetWeight,
                item.DeltaWeight,
                item.IsInUniverse,
                item.WithinTradeBuffer,
                item.HasTradableMarket))
            .ToArray();

        return new EffectiveWeightsVerification(
            plan.WeightConstraint,
            CalculateGrossExposure(effectiveItems, item => item.CurrentWeight),
            CalculateNetExposure(effectiveItems, item => item.CurrentWeight),
            CalculateGrossExposure(effectiveItems, item => item.ConstrainedTargetWeight),
            CalculateNetExposure(effectiveItems, item => item.ConstrainedTargetWeight),
            CalculateGrossExposure(effectiveItems, item => item.EffectiveWeight),
            CalculateNetExposure(effectiveItems, item => item.EffectiveWeight),
            effectiveItems);
    }

    private static decimal? CalculateGrossExposure(
        IReadOnlyList<EffectiveWeightItem> items,
        Func<EffectiveWeightItem, decimal?> selectWeight) =>
        TryGetCompleteWeights(items, selectWeight, out var weights)
            ? weights.Sum(Math.Abs)
            : null;

    private static decimal? CalculateNetExposure(
        IReadOnlyList<EffectiveWeightItem> items,
        Func<EffectiveWeightItem, decimal?> selectWeight) =>
        TryGetCompleteWeights(items, selectWeight, out var weights)
            ? weights.Sum()
            : null;

    private static bool TryGetCompleteWeights(
        IReadOnlyList<EffectiveWeightItem> items,
        Func<EffectiveWeightItem, decimal?> selectWeight,
        out decimal[] weights)
    {
        var selected = items.Select(selectWeight).ToArray();
        if (selected.Any(weight => !weight.HasValue))
        {
            weights = [];
            return false;
        }

        weights = [.. selected.Select(weight => weight.GetValueOrDefault())];
        return true;
    }

    private sealed class DuplicateBaseAssetWeightsException(string message) : InvalidOperationException(message);

    private sealed record EffectiveWeightsVerification(
        decimal WeightConstraint,
        decimal? CurrentGrossExposure,
        decimal? CurrentNetExposure,
        decimal? EffectiveGrossExposure,
        decimal? EffectiveNetExposure,
        decimal? BufferAdjustedGrossExposure,
        decimal? BufferAdjustedNetExposure,
        IReadOnlyList<EffectiveWeightItem> Weights);

}
