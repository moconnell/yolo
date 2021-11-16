using System;
using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;
using YoloAbstractions.Extensions;

namespace YoloTrades
{
    public static class YoloTradesExtensions
    {
        public static IEnumerable<Trade> CalculateTrades(
            this IEnumerable<Weight> weights,
            IDictionary<string, Position> positions,
            IDictionary<string, IEnumerable<Price>> prices,
            IDictionary<string, SymbolInfo> symbols,
            decimal tradeBuffer,
            decimal maxLeverage,
            decimal? nominalCash,
            string baseCurrencyToken,
            AssetTypePreference tradePreference = AssetTypePreference.MatchExistingPosition)
        {
            var nominal = nominalCash ??
                          positions.GetTotalValue(prices, baseCurrencyToken);
            var weightsList = weights.ToList();

            var unconstrainedTargetLeverage = weightsList
                .Select(w => Math.Abs(w.ComboWeight))
                .Sum();

            var weightConstraint = unconstrainedTargetLeverage < maxLeverage
                ? 1
                : maxLeverage / unconstrainedTargetLeverage;

            Console.WriteLine("SETTINGS");
            Console.WriteLine();
            Console.WriteLine($"trade buffer: {tradeBuffer:0.00}");
            Console.WriteLine($"max leverage: {maxLeverage:0.00}");
            Console.WriteLine($"nominal cash allocation: {nominal:0,000.00}");
            Console.WriteLine();

            Console.WriteLine("CURRENT HOLDINGS");
            Console.WriteLine();

            positions.ToList()
                .ForEach(pair => Console.WriteLine(pair.Value));
            Console.WriteLine();

            Console.WriteLine("CURRENT PRICES");
            Console.WriteLine();

            prices
                .OrderBy(kvp => kvp.Key)
                .ToList()
                .ForEach(pair =>
                    Console.WriteLine(
                        $"{pair.Key}: {pair.Value.Select(p => $"{p.Last} ({p.AssetType})").ToCsv()}"));
            Console.WriteLine();

            var trades = new List<Trade>();

            void AddTrade(Weight w)
            {
                Console.WriteLine();
                Console.WriteLine("**********************************************************");

                var (token, baseCurrency) = w.Ticker.SplitConstituents();

                // var baseCurrencyHoldings = positions
                //     .TryGetValue(baseCurrencyToken, out var pos)
                //     ? (baseCurrencyToken, baseCurrencyTokenWeight: pos.Amount)
                //     : (baseCurrencyToken, 0);

                var (_, assetType, amount) = positions.TryGetValue(token, out var position)
                    ? position
                    : Position.Null;
                var constrainedTargetWeight = weightConstraint * w.ComboWeight;

                Console.WriteLine(w.Ticker);
                Console.WriteLine();

                var priceAssetTypeFilter =
                    tradePreference == AssetTypePreference.MatchExistingPosition && amount != 0
                        ? assetType
                        : (AssetType?)null;

                var cross = $"{token}{baseCurrencyToken}";
                var crossPrices = prices.GetPrices(
                        cross,
                        priceAssetTypeFilter)
                    .OrderBy(p => p.Last)
                    .Select(p => (price: p, currentWeight: amount * p.Last / nominal))
                    .ToArray();

                if (!crossPrices.Any())
                {
                    var priceAssetType = priceAssetTypeFilter.HasValue
                        ? $" ({priceAssetTypeFilter})"
                        : string.Empty;
                    Console.WriteLine($"No prices for {cross}{priceAssetType}!");
                    return;
                }

                var (price, currentWeight) =
                    constrainedTargetWeight - crossPrices.Last()
                        .currentWeight < 0
                        ? crossPrices.Last()
                        : crossPrices.First();

                Console.WriteLine($"current position: {amount:0.0000} ({assetType})");
                Console.WriteLine($"current weight: {currentWeight:0.0000}");
                Console.WriteLine($"momo: {w.MomentumFactor:0.0000}");
                Console.WriteLine($"trend: {w.TrendFactor:0.0000}");
                Console.WriteLine($"unconstrained target weight: {w.ComboWeight:0.0000}");
                Console.WriteLine($"constrained target weight: {constrainedTargetWeight:0.0000}");
                Console.WriteLine($"best price: {price}");

                if (currentWeight >= constrainedTargetWeight - tradeBuffer &&
                    currentWeight <= constrainedTargetWeight + tradeBuffer)
                {
                    Console.WriteLine("delta: within trade buffer");
                    return;
                }

                var symbol = symbols[$"{price.AssetName}-{price.AssetType}"];

                var tbf = constrainedTargetWeight < currentWeight ? -1 : 1;

                var rawDelta = (constrainedTargetWeight - tbf * tradeBuffer - currentWeight) *
                    nominal / price.Last;

                var delta = Math.Floor(rawDelta / symbol.LotSizeStepSize) *
                            symbol.LotSizeStepSize;

                Console.WriteLine($"delta: {delta:0.0000}");

                trades.Add(new Trade(price.AssetName, price.AssetType, delta));
            }

            weightsList
                .ForEach(AddTrade);

            return trades;
        }

    }
}