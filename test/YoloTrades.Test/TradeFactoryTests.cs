using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Snapshooter.Xunit;
using Xunit;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloTrades.Test.Data.Types;

namespace YoloTrades.Test;

public class TradeFactoryTests
{
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
        decimal stepSize = 0.0001m)
    {
        var mockLogger = new Mock<ILogger<TradeFactory>>();

        var config = new YoloConfig
        {
            BaseAsset = baseCurrency,
            NominalCash = nominalCash,
            TradeBuffer = tradeBuffer,
            WeightingMomentumFactor = momentumMultiplier,
            WeightingTrendFactor = trendMultiplier,
            WeightingCarryFactor = carryMultiplier
        };
        var tradeFactory = new TradeFactory(mockLogger.Object, config);

        var (weights, positions, markets, expectedTrades) =
            DeserializeCsv(path, baseCurrency, stepSize);

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

    [Theory(Skip = "Needs updating")]
    [InlineData("./Data/json/001")]
    [InlineData("./Data/json/003_ExistingPositions")]
    [InlineData("./Data/json/004_TokenUniverseChange")]
    public async Task ShouldCalculateTrades(
        string path,
        AssetPermissions assetPermissions = AssetPermissions.All,
        string baseCurrency = "USD",
        decimal nominalCash = 10000,
        decimal tradeBuffer = 0.04m)
    {
        var mockLogger = new Mock<ILogger<TradeFactory>>();

        var config = new YoloConfig
        {
            AssetPermissions = assetPermissions,
            BaseAsset = baseCurrency,
            NominalCash = nominalCash,
            TradeBuffer = tradeBuffer
        };
        var tradeFactory = new TradeFactory(mockLogger.Object, config);

        var (weights, positions, markets) = await DeserializeInputsAsync(path);

        var trades = tradeFactory.CalculateTrades(weights, positions, markets);

        Assert.NotNull(trades);

        var directory = path[(path.LastIndexOf('/') + 1)..];
        trades.MatchSnapshot($"ShouldCalculateTrades_{directory}");
    }

    private static
        (Weight[] weights,
        Dictionary<string, IReadOnlyList<Position>> positions,
        Dictionary<string, IReadOnlyList<MarketInfo>> markets,
        Dictionary<string, decimal> expectedTrades)
        DeserializeCsv(string path, string baseCurrency, decimal stepSize)
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

        var weights = records.Select(x =>
                new Weight(
                    x.Price,
                    x.Carry,
                    DateTime.Today,
                    x.Momentum,
                    $"{x.Ticker}/{baseCurrency}",
                    x.Trend,
                    x.Volatility))
            .ToArray();

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
    }

    private static async
        Task<(Weight[], Dictionary<string, IReadOnlyList<Position>>, Dictionary<string, IReadOnlyList<MarketInfo>>)>
        DeserializeInputsAsync(
            string path)
    {
        var weights = await DeserializeAsync<Weight[]>($"{path}/weights.json");

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