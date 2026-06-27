using Moq;
using Shouldly;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Interfaces;
using YoloBroker.Interface;
using YoloTrades;

namespace YoloTrades.Test;

public class TradeAdvisorTest
{
    private static readonly IReadOnlyDictionary<string, decimal> Weights =
        new Dictionary<string, decimal> { ["SOL"] = 0.5m };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Position>> EmptyPositions =
        new Dictionary<string, IReadOnlyList<Position>>();

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>> EmptyMarkets =
        new Dictionary<string, IReadOnlyList<MarketInfo>>();

    private static readonly Trade TimedOutTrade = new(
        Symbol: "SOL",
        AssetType: AssetType.Future,
        Amount: 5m,
        LimitPrice: 100m,
        OrderType: OrderType.Limit,
        ClientOrderId: "orig-1");

    [Fact]
    public async Task WhenTradeFactoryReturnsMatchingTrade_ShouldReturnIt()
    {
        var replacementTrade = TimedOutTrade with { OrderType = OrderType.Market, LimitPrice = null };

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPositions);
        broker.Setup(x => x.GetMarketsAsync(
                It.IsAny<ISet<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyMarkets);

        var factory = new Mock<ITradeFactory>();
        factory.Setup(x => x.CalculateTrades(
                It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns([replacementTrade]);

        var sut = new TradeAdvisor(Weights, factory.Object, broker.Object, "USDC", AssetPermissions.All);

        var result = await sut.GetReplacementTradeAsync(TimedOutTrade);

        result.ShouldBe(replacementTrade);
    }

    [Fact]
    public async Task WhenTradeFactoryProducesNoTradeForSymbol_ShouldReturnNull()
    {
        var otherTrade = TimedOutTrade with { Symbol = "ETH" };

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPositions);
        broker.Setup(x => x.GetMarketsAsync(
                It.IsAny<ISet<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyMarkets);

        var factory = new Mock<ITradeFactory>();
        factory.Setup(x => x.CalculateTrades(
                It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns([otherTrade]);

        var sut = new TradeAdvisor(Weights, factory.Object, broker.Object, "USDC", AssetPermissions.All);

        var result = await sut.GetReplacementTradeAsync(TimedOutTrade);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ShouldFetchMarketsForRelevantBaseAssets()
    {
        var positions = new Dictionary<string, IReadOnlyList<Position>>
        {
            ["ETH"] = [new Position("ETH", "ETH", AssetType.Future, 1m)]
        };

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);
        broker.Setup(x => x.GetMarketsAsync(
                It.IsAny<ISet<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyMarkets);

        var factory = new Mock<ITradeFactory>();
        factory.Setup(x => x.CalculateTrades(
                It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns(Array.Empty<Trade>());

        var sut = new TradeAdvisor(Weights, factory.Object, broker.Object, "USDC", AssetPermissions.All);

        await sut.GetReplacementTradeAsync(TimedOutTrade);

        broker.Verify(x => x.GetMarketsAsync(
            It.Is<ISet<string>>(filter =>
                filter.SetEquals(new[] { "ETH", "SOL" })),
            "USDC",
            AssetPermissions.All,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShouldPassWeightsAndBrokerDataToTradeFactory()
    {
        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPositions);
        broker.Setup(x => x.GetMarketsAsync(
                It.IsAny<ISet<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyMarkets);

        var factory = new Mock<ITradeFactory>();
        factory.Setup(x => x.CalculateTrades(
                It.IsAny<IReadOnlyDictionary<string, decimal>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Position>>>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<MarketInfo>>>()))
            .Returns(Array.Empty<Trade>());

        var sut = new TradeAdvisor(Weights, factory.Object, broker.Object, "USDC", AssetPermissions.All);

        await sut.GetReplacementTradeAsync(TimedOutTrade);

        factory.Verify(x => x.CalculateTrades(
            Weights,
            EmptyPositions,
            EmptyMarkets), Times.Once);
    }
}
