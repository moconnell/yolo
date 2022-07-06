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
using Snapshooter.Xunit;
using Xunit;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloTestUtils;
using YoloTrades.Test.Data.Types;

namespace YoloTrades.Test;

public class TradeFactoryTests
{
    [Theory]
    [InlineData("./Data/csv/20211212/YOLO Trade Helper v4 20211212.csv")]
    public void ShouldCalculateTradesFromCsv(
        string path,
        string baseCurrency = "USD",
        decimal nominalCash = 10000,
        decimal tradeBuffer = 0.02m,
        decimal stepSize = 0.0001m)
    {
        var mockLogger = new Mock<ILogger<TradeFactory>>();

        var config = new YoloConfig
        {
            BaseAsset = baseCurrency,
            NominalCash = nominalCash,
            TradeBuffer = tradeBuffer
        };
        var tradeFactory = new TradeFactory(mockLogger.Object, config);

        var (weights, positions, markets, expectedTrades) =
            DeserializeCsv(path, baseCurrency, stepSize);

        var groupings = tradeFactory
            .CalculateTrades(weights, positions, markets)
            .ToArray();

        Assert.NotNull(groupings);

        var filename = path[(path.LastIndexOf("/", StringComparison.Ordinal) + 1)..]
            .Replace(" ", string.Empty);
        var trades = groupings.SelectMany(g => g.ToArray());
        trades.MatchSnapshot($"ShouldCalculateTradesFromCsv_{filename}", options => options.IgnoreFields("[*].Id"));

        Assert.Equal(expectedTrades.Count, groupings.Length);

        var tradesWithDeviatingQuantity = groupings
            .Select(
                g =>
                {
                    var baseAsset = g.Key;
                    var expectedTradeQuantity = expectedTrades[baseAsset];
                    var amount = g.Sum(t => t.Amount);

                    return (baseAsset, expectedTradeQuantity, amount,
                        deviation: amount - expectedTradeQuantity);
                })
            .Where(tuple => Math.Abs(tuple.deviation) > stepSize)
            .ToArray();

        Assert.Empty(tradesWithDeviatingQuantity);
    }

    [Theory]
    [InlineData("./Data/json/001")]
    [InlineData("./Data/json/002_LongSpotAndPerp", AssetPermissions.LongSpotAndPerp)]
    [InlineData("./Data/json/003_ExistingPositions")]
    [InlineData("./Data/json/004_TokenUniverseChange")]
    [InlineData("./Data/json/005_MinimumProvide", AssetPermissions.LongSpotAndPerp, "USD", 500, 0.02)]
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

        var groupings = tradeFactory.CalculateTrades(weights, positions, markets);

        Assert.NotNull(groupings);

        var directory = path[(path.LastIndexOf("/", StringComparison.InvariantCulture) + 1)..];
        var trades = groupings.SelectMany(g => g.ToArray());
        trades.MatchSnapshot($"ShouldCalculateTrades_{directory}", options => options.IgnoreFields("[*].Id"));
    }

    private static
        (Dictionary<string, Weight> weights,
        Dictionary<string, IEnumerable<Position>> positions,
        Dictionary<string, IEnumerable<MarketInfo>> markets,
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

        var weights = records.ToDictionary(
            x => x.Ticker,
            x =>
                new Weight(
                    x.Price,
                    (x.Momentum + x.Trend) / 2,
                    DateTime.Today,
                    x.Momentum,
                    $"{x.Ticker}/{baseCurrency}",
                    x.Trend));

        var positions = records.ToDictionary(
            x => x.Ticker,
            x =>
                new[]
                {
                    new Position(
                        $"{x.Ticker}/{baseCurrency}",
                        x.Ticker,
                        AssetType.Spot,
                        x.CurrentPosition)
                }.Cast<Position>());

        var markets = records.ToDictionary(
            x => x.Ticker,
            x =>
                new[]
                {
                    new MarketInfo(
                        $"{x.Ticker}/{baseCurrency}",
                        x.Ticker,
                        baseCurrency,
                        AssetType.Spot,
                        stepSize,
                        stepSize,
                        0,
                        x.Price,
                        x.Price,
                        x.Price,
                        null,
                        DateTime.UtcNow)
                } as IEnumerable<MarketInfo>);

        var expectedTrades = records
            .Where(x => x.TradeQuantity != 0)
            .ToDictionary(
                x => x.Ticker,
                x => x.TradeQuantity);

        return (weights, positions, markets, expectedTrades);
    }

    private static async
        Task<(Dictionary<string, Weight>,
            Dictionary<string, IEnumerable<Position>>,
            Dictionary<string, IEnumerable<MarketInfo>>)>
        DeserializeInputsAsync(
            string path)
    {
        var weights = (await $"{path}/weights.json".DeserializeAsync<Weight[]>())
            .ToDictionary(x => x.BaseAsset);

        var positions =
            (await $"{path}/positions.json".DeserializeAsync<Dictionary<string, Position[]>>())
            .ToEnumerableDictionary();

        var markets =
            (await $"{path}/markets.json".DeserializeAsync<Dictionary<string, MarketInfo[]>>())
            .ToEnumerableDictionary();

        return (weights, positions, markets);
    }
}