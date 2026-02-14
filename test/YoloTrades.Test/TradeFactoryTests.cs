using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MathNet.Numerics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Snapshooter.Xunit;
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
        var tradeFactory = new TradeFactory(config, logger);

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
                var expectedTradeQuantity = expectedTrades[baseAsset].RoundToMultiple(stepSize);

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
        var tradeFactory = new TradeFactory(config, logger);

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
        var tradeFactory = new TradeFactory(config, logger);

        var (weights, positions, markets) = await DeserializeInputsAsync(path);

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

    [Theory]
    [InlineData("./Data/json/009_UniverseChange_SmallPosition", 0.04)]
    [InlineData("./Data/json/009_UniverseChange_SmallPosition", 0.02)]
    public async Task GivenUniverseChange_ShouldClosePositionRegardlessOfToleranceBand(
        string path,
        decimal tradeBuffer)
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();

        var config = new YoloConfig
        {
            AssetPermissions = AssetPermissions.All,
            BaseAsset = "USDC",
            MaxLeverage = 2,
            NominalCash = 10000,
            TradeBuffer = tradeBuffer,
            MinOrderValue = null // No minimum order value restriction
        };
        var tradeFactory = new TradeFactory(config, logger);

        var (weights, positions, markets) = await DeserializeInputsAsync(path);

        // Calculate what the position weight would be
        // MNT position: 50 tokens at $1.195 = $59.75
        // Current weight: $59.75 / $10000 = 0.005975 (~0.6%)
        // This is well within typical trade buffers (2-4%)

        // act
        var trades = tradeFactory.CalculateTrades(weights, positions, markets).ToArray();

        // assert
        Assert.NotNull(trades);

        // Should generate a trade to close the MNT position even though it's small
        var mntTrade = trades.FirstOrDefault(t => t.Symbol == "MNT");
        Assert.NotNull(mntTrade);

        // The trade should close the entire MNT position (sell 50 MNT)
        mntTrade.Amount.ShouldBe(-50.0m, 0.1m);
        mntTrade.OrderSide.ShouldBe(OrderSide.Sell);
    }

    [Theory]
    [InlineData("./Data/json/010_UniverseChange_BelowMinOrderValue", 10.0)]
    public async Task GivenUniverseChange_ShouldClosePositionEvenWhenBelowMinOrderValue(
        string path,
        decimal minOrderValue)
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();

        var config = new YoloConfig
        {
            AssetPermissions = AssetPermissions.All,
            BaseAsset = "USDC",
            MaxLeverage = 2,
            NominalCash = 10000,
            TradeBuffer = 0.04m,
            MinOrderValue = minOrderValue // Set minimum order value
        };
        var tradeFactory = new TradeFactory(config, logger);

        var (weights, positions, markets) = await DeserializeInputsAsync(path);

        // Calculate what the position value is
        // MNT position: 5 tokens at ~$1.19 = $5.95
        // This is below the MinOrderValue of $10
        // But since MNT dropped from universe, it should still be closed

        // act
        var trades = tradeFactory.CalculateTrades(weights, positions, markets).ToArray();

        // assert
        Assert.NotNull(trades);

        // Should generate a trade to close MNT even though order value is below MinOrderValue
        // because the token dropped from the universe
        var mntTrade = trades.FirstOrDefault(t => t.Symbol == "MNT");
        Assert.NotNull(mntTrade);

        // The trade should close the entire MNT position (sell 5 MNT)
        mntTrade.Amount.ShouldBe(-5.0m, 0.1m);
        mntTrade.OrderSide.ShouldBe(OrderSide.Sell);
    }

    [Theory]
    [InlineData(1.001, -1.001)]
    [InlineData(0.01, -0.01)]
    public void GivenDroppedTokenLongSpot_WhenReduceOnlyAndQuantityStepMisaligned_ShouldNotOverClose(
        decimal currentPosition,
        decimal expectedTradeAmount)
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();
        var config = new YoloConfig
        {
            AssetPermissions = AssetPermissions.All,
            BaseAsset = "USDC",
            NominalCash = 10000,
            TradeBuffer = 0.04m
        };

        var tradeFactory = new TradeFactory(config, logger);

        var weights = new Dictionary<string, decimal>();
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["ABC"] =
            [
                new Position("ABC", "ABC", AssetType.Spot, currentPosition)
            ]
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["ABC"] =
            [
                new MarketInfo(
                    "ABC/USDC",
                    "ABC",
                    "USDC",
                    AssetType.Spot,
                    DateTime.UtcNow,
                    PriceStep: 0.01m,
                    QuantityStep: 0.1m,
                    MinProvideSize: 0,
                    Ask: 100.1m,
                    Bid: 100.0m,
                    Last: 100.05m)
            ]
        };

        // act
        var trades = tradeFactory.CalculateTrades(weights, positions, markets).ToArray();

        // assert
        trades.Length.ShouldBe(1);
        var trade = trades[0];
        trade.Symbol.ShouldBe("ABC/USDC");
        trade.ReduceOnly.ShouldBe(true);
        trade.Amount.ShouldBe(expectedTradeAmount);
        Math.Abs(trade.Amount).ShouldBeLessThanOrEqualTo(Math.Abs(currentPosition));
    }

    [Theory]
    [InlineData(-1.001, 1.001)]
    [InlineData(-0.01, 0.01)]
    public void GivenDroppedTokenShortSpot_WhenReduceOnlyAndQuantityStepMisaligned_ShouldNotOverClose(
        decimal currentPosition,
        decimal expectedTradeAmount)
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();
        var config = new YoloConfig
        {
            AssetPermissions = AssetPermissions.All,
            BaseAsset = "USDC",
            NominalCash = 10000,
            TradeBuffer = 0.04m
        };

        var tradeFactory = new TradeFactory(config, logger);

        var weights = new Dictionary<string, decimal>();
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["ABC"] =
            [
                new Position("ABC", "ABC", AssetType.Spot, currentPosition)
            ]
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["ABC"] =
            [
                new MarketInfo(
                    "ABC/USDC",
                    "ABC",
                    "USDC",
                    AssetType.Spot,
                    DateTime.UtcNow,
                    PriceStep: 0.01m,
                    QuantityStep: 0.1m,
                    MinProvideSize: 0,
                    Ask: 100.1m,
                    Bid: 100.0m,
                    Last: 100.05m)
            ]
        };

        // act
        var trades = tradeFactory.CalculateTrades(weights, positions, markets).ToArray();

        // assert
        trades.Length.ShouldBe(1);
        var trade = trades[0];
        trade.Symbol.ShouldBe("ABC/USDC");
        trade.ReduceOnly.ShouldBe(true);
        trade.Amount.ShouldBe(expectedTradeAmount);
        Math.Abs(trade.Amount).ShouldBeLessThanOrEqualTo(Math.Abs(currentPosition));
    }

    [Fact]
    public void GivenDroppedTokenLongSpot_WhenQuantityStepAligned_ShouldNotOverClose()
    {
        // arrange
        var logger = _loggerFactory.CreateLogger<TradeFactory>();
        var config = new YoloConfig
        {
            AssetPermissions = AssetPermissions.All,
            BaseAsset = "USDC",
            NominalCash = 10000,
            TradeBuffer = 0.04m
        };

        var tradeFactory = new TradeFactory(config, logger);

        var weights = new Dictionary<string, decimal>();
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["ABC"] =
            [
                new Position("ABC", "ABC", AssetType.Spot, 1.1m)
            ]
        };
        var markets = new Dictionary<string, IReadOnlyList<MarketInfo>>
        {
            ["ABC"] =
            [
                new MarketInfo(
                    "ABC/USDC",
                    "ABC",
                    "USDC",
                    AssetType.Spot,
                    DateTime.UtcNow,
                    PriceStep: 0.01m,
                    QuantityStep: 0.1m,
                    MinProvideSize: 0,
                    Ask: 100.1m,
                    Bid: 100.0m,
                    Last: 100.05m)
            ]
        };

        // act
        var trades = tradeFactory.CalculateTrades(weights, positions, markets).ToArray();

        // assert
        trades.Length.ShouldBe(1);
        var trade = trades[0];
        trade.ReduceOnly.ShouldBe(true);
        trade.Amount.ShouldBe(-1.1m);
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