using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Snapshooter.Xunit;
using Xunit;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloTrades;

namespace YoloTrades.Test;

public class TradeFactoryTests
{
    [Theory]
    [InlineData("./Data/001")]
    public async Task ShouldCalculateTrades(
        string path,
        string baseAsset = "USD",
        decimal nominalCash = 10000,
        decimal tradeBuffer = 0.04m)
    {
        var mockLogger = new Mock<ILogger<TradeFactory>>();
        var config = new YoloConfig
        {
            BaseAsset = baseAsset,
            NominalCash = nominalCash,
            TradeBuffer = tradeBuffer
        };
        var tradeFactory = new TradeFactory(mockLogger.Object, config);

        var (weights, positions, markets) = await DeserializeInputs(path);

        var trades = tradeFactory.CalculateTrades(weights, positions, markets);

        Assert.NotNull(trades);
        trades.MatchSnapshot();
    }

    private static async
        Task<(Weight[], Dictionary<string, Position>, Dictionary<string, IEnumerable<MarketInfo>>)>
        DeserializeInputs(
            string path)
    {
        var weights = await DeserializeAsync<Weight[]>($"{path}/weights.json");

        var positions =
            await DeserializeAsync<Dictionary<string, Position>>($"{path}/positions.json");

        var markets =
            await DeserializeAsync<Dictionary<string, MarketInfo[]>>($"{path}/markets.json");

        var markets2 = markets.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Cast<MarketInfo>());

        return (weights, positions, markets2);
    }

    private static async Task<T> DeserializeAsync<T>(string path)
    {
        await using var stream = File.OpenRead(path);
        using var streamReader = new StreamReader(stream);
        var json = await streamReader.ReadToEndAsync();

        return JsonConvert.DeserializeObject<T>(json);
    }
}