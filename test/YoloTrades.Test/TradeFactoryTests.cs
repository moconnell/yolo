using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shouldly;
using Snapshooter.Xunit;
using Xunit;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloTest.Util;
using YoloTrades.Test.Data.Types;

namespace YoloTrades.Test;

public class TradeFactoryTests
{
    private readonly ILoggerFactory _loggerFactory;

    public TradeFactoryTests(ITestOutputHelper testOutputHelper)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(testOutputHelper));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    [Theory]
    // [InlineData("./Data/csv/20250907/YOLO Trade Helper v8 - carry=0.csv", 1, 1, 0)]
    // [InlineData("./Data/csv/20250907/YOLO Trade Helper v8 - carry=0.5.csv", 1, 1, 0.5)]
    [InlineData("./Data/csv/20250907/YOLO Trade Helper v8 - w positions, carry=0.5.csv", 4, 1, 1, 0.5)]
    public void ShouldCalculateTradesFromCsv(
        string path,
        int expectedNumberOfTrades,
        decimal momentumMultiplier = 1,
        decimal trendMultiplier = 1,
        decimal carryMultiplier = 1,
        string baseCurrency = "USDC",
        decimal nominalCash = 10000,
        decimal tradeBuffer = 0.02m,
        decimal stepSize = 0.0001m,
        decimal maxWeightingAbs = 0.25m)
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();

        var config = new YoloConfig
        {
            BaseAsset = baseCurrency,
            NominalCash = nominalCash,
            TradeBuffer = tradeBuffer,
            FactorWeights = new Dictionary<FactorType, decimal>
            {
                { FactorType.Momentum, momentumMultiplier },
                { FactorType.Trend, trendMultiplier },
                { FactorType.Carry, carryMultiplier }
            }
        };
        var tradeFactory = new TradeFactory(logger, config);

        var (weights, positions, markets, expectedTrades) =
            DeserializeCsv(
                path,
                baseCurrency,
                stepSize,
                momentumMultiplier,
                trendMultiplier,
                carryMultiplier,
                maxWeightingAbs);

        var trades = tradeFactory
            .CalculateTrades(weights, positions, markets)
            .ToArray();

        Assert.NotNull(trades);

        var filename = path[(path.LastIndexOf('/') + 1)..]
            .Replace(" ", string.Empty);
        trades.MatchSnapshot($"ShouldCalculateTradesFromCsv_{filename}");

        Assert.Equal(expectedNumberOfTrades, trades.Length);

        var tradesWithDeviatingQuantity = trades
            .Select(t =>
            {
                var baseAsset = t.Symbol.Split('-', '/')[0];
                var expectedTradeQuantity = expectedTrades[baseAsset];

                return (baseAsset, expectedTradeQuantity, t.Amount,
                    deviation: t.Amount - expectedTradeQuantity);
            })
            .Where(tuple => Math.Abs(tuple.deviation) > stepSize)
            .ToArray();

        Assert.Empty(tradesWithDeviatingQuantity);
    }

    [Theory]
    [InlineData("./Data/json/001")]
    [InlineData("./Data/json/003_ExistingPositions")]
    [InlineData("./Data/json/004_TokenUniverseChange")]
    public async Task ShouldCalculateTrades(
        string path,
        AssetPermissions assetPermissions = AssetPermissions.All,
        string baseCurrency = "USDC",
        decimal maxLeverage = 2,
        decimal nominalCash = 10000,
        decimal tradeBuffer = 0.04m)
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();

        var config = new YoloConfig
        {
            AssetPermissions = assetPermissions,
            BaseAsset = baseCurrency,
            MaxLeverage = maxLeverage,
            NominalCash = nominalCash,
            TradeBuffer = tradeBuffer
        };
        var tradeFactory = new TradeFactory(logger, config);

        var (weights, positions, markets) = await DeserializeInputsAsync(path);

        // act
        var trades = tradeFactory.CalculateTrades(weights, positions, markets);

        // assert
        Assert.NotNull(trades);

        var directory = path[(path.LastIndexOf('/') + 1)..];
        trades.MatchSnapshot($"ShouldCalculateTrades_{directory}");
    }

    [Theory]
    [InlineData(
        "./Data/json/005_EdgeRebalance",
        RebalanceMode.Center,
        "BTC",
        0.017,
        0.00001)]
    [InlineData(
        "./Data/json/005_EdgeRebalance",
        RebalanceMode.Edge,
        "BTC",
        0.009,
        0.00001)]
    [InlineData(
        "./Data/json/006_EdgeRebalanceOverweight",
        RebalanceMode.Center,
        "BTC",
        -0.02,
        0.00001)]
    [InlineData(
        "./Data/json/006_EdgeRebalanceOverweight",
        RebalanceMode.Edge,
        "BTC",
        -0.012,
        0.00001)]
    [InlineData(
        "./Data/json/007_EdgeRebalanceOverweight_CrossingZeroBoundary",
        RebalanceMode.Center,
        "BTC",
        -0.06,
        0.00001)]
    [InlineData(
        "./Data/json/007_EdgeRebalanceOverweight_CrossingZeroBoundary",
        RebalanceMode.Edge,
        "BTC",
        -0.052,
        0.00001)]
    [InlineData(
        "./Data/json/008_EdgeRebalanceOverweight_CrossingZeroBoundary",
        RebalanceMode.Center,
        "DOGE",
        -13493,
        1,
        44000,
        0.05)]
    [InlineData(
        "./Data/json/008_EdgeRebalanceOverweight_CrossingZeroBoundary",
        RebalanceMode.Edge,
        "DOGE",
        -1254,
        1,
        44000,
        0.05)]
    public async Task GivenRebalanceMode_ShouldCalculateTrades(
        string path,
        RebalanceMode rebalanceMode,
        string expectedSymbol,
        decimal expectedTradeSize,
        decimal tolerance,
        decimal nominalCash = 10000,
        decimal tradeBuffer = 0.04m,
        decimal maxLeverage = 2)
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();

        var config = new YoloConfig
        {
            AssetPermissions = AssetPermissions.PerpetualFutures,
            BaseAsset = "USDC",
            MaxLeverage = maxLeverage,
            NominalCash = nominalCash,
            TradeBuffer = tradeBuffer,
            RebalanceMode = rebalanceMode
        };
        var tradeFactory = new TradeFactory(logger, config);

        var (weights, positions, markets) =
            await DeserializeInputsAsync(path);

        // act
        var trades = tradeFactory.CalculateTrades(weights, positions, markets).ToArray();

        // assert
        Assert.NotNull(trades);

        /*
         e.g.

         Current position: 0.04 BTC at $50,000 = $2,000
         Current weight = $2,000 / $10,000 = 0.20 = 20%
         Target weight = -0.10 = -10%
         Trade buffer = 0.04 = 4%
         Tolerance band = [-6%, -14%]
         Current 20% is outside (above) the band, so we need to rebalance

         For Center mode: rebalance to -10% (ideal weight)
           Target position = -0.10 * 10000 / 50000 = -0.02 BTC
           Trade size = -0.02 - 0.04 = -0.06 BTC (sell)

         For Edge mode: rebalance to upper edge at -6%
           Target position = -0.06 * 10000 / 50000 = -0.012 BTC
           Trade size = -0.012 - 0.04 = -0.052 BTC (sell)
        */

        Assert.Single(trades);
        var trade = trades[0];
        trade.Symbol.ShouldBe(expectedSymbol);
        trade.Amount.ShouldBe(expectedTradeSize, tolerance);
    }

    private static
        (Dictionary<string, decimal> weights,
        Dictionary<string, IReadOnlyList<Position>> positions,
        Dictionary<string, IReadOnlyList<MarketInfo>> markets,
        Dictionary<string, decimal> expectedTrades)
        DeserializeCsv(
            string path,
            string baseCurrency,
            decimal stepSize,
            decimal momentumMultiplier,
            decimal trendMultiplier,
            decimal carryMultiplier,
            decimal maxWeightingAbs)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);

        var records = csv
            .GetRecords<YoloCsvRow>()
            .ToArray();

        var weights = records.ToDictionary(x => $"{x.Ticker}/{baseCurrency}", ToWeight);

        var positions = records.ToDictionary(
            x => x.Ticker,
            IReadOnlyList<Position> (x) =>
            [
                new(
                    $"{x.Ticker}/{baseCurrency}",
                    x.Ticker,
                    AssetType.Future,
                    x.CurrentPosition)
            ]);

        var markets = records.ToDictionary(
            x => x.Ticker,
            IReadOnlyList<MarketInfo> (x) =>
            [
                new(
                    $"{x.Ticker}/{baseCurrency}",
                    x.Ticker,
                    baseCurrency,
                    AssetType.Future,
                    DateTime.UtcNow,
                    stepSize,
                    stepSize,
                    0,
                    x.Price,
                    x.Price,
                    x.Price
                )
            ]);

        var expectedTrades = records
            .Where(x => x.TradeQuantity != 0)
            .ToDictionary(
                x => x.Ticker,
                x => x.TradeQuantity);

        return (weights, positions, markets, expectedTrades);

        decimal ToWeight(YoloCsvRow row)
        {
            var divisor = (carryMultiplier > 0 ? 1 : 0) +
                          (momentumMultiplier > 0 ? 1 : 0) +
                          (trendMultiplier > 0 ? 1 : 0);

            if (divisor == 0)
            {
                return 0m;
            }

            var volatility = row.Volatility > 0 ? row.Volatility : 1;
            var volAdjustedWeight = (row.Carry * carryMultiplier +
                                     row.Momentum * momentumMultiplier +
                                     row.Trend * trendMultiplier) /
                                    (divisor * volatility);
            var clampedWeight = Math.Clamp(volAdjustedWeight, -maxWeightingAbs, maxWeightingAbs);

            return clampedWeight;
        }
    }

    private static async Task<(Dictionary<string, decimal>,
            Dictionary<string, IReadOnlyList<Position>>,
            Dictionary<string, IReadOnlyList<MarketInfo>>)>
        DeserializeInputsAsync(string path)
    {
        var weights = await DeserializeAsync<Dictionary<string, decimal>>($"{path}/weights.json");

        var positions =
            ToEnumerableDictionary(await DeserializeAsync<Dictionary<string, Position[]>>($"{path}/positions.json"));

        var markets =
            ToEnumerableDictionary(await DeserializeAsync<Dictionary<string, MarketInfo[]>>($"{path}/markets.json"));

        return (weights, positions, markets);
    }

    private static Dictionary<TKey, IReadOnlyList<TValue>> ToEnumerableDictionary<TKey, TValue>(
        IDictionary<TKey, TValue[]> dictionary) where TKey : notnull
    {
        return dictionary.ToDictionary(
            kvp => kvp.Key,
            IReadOnlyList<TValue> (kvp) => kvp.Value);
    }

    private static async Task<T> DeserializeAsync<T>(string path)
    {
        await using var stream = File.OpenRead(path);
        using var streamReader = new StreamReader(stream);
        var json = await streamReader.ReadToEndAsync();

        return JsonConvert.DeserializeObject<T>(json)!;
    }
}