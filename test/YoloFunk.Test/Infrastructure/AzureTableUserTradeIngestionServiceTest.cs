using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Microsoft.Extensions.Logging.Abstractions;
using YoloAbstractions;
using YoloBroker.Interface;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Infrastructure;

public sealed class AzureTableUserTradeIngestionServiceTest
{
    [Fact]
    public async Task IngestAsync_WhenNoWatermark_ShouldStoreTradesWithDeterministicKeys()
    {
        var capturedTrades = new List<TableEntity>();
        var capturedStates = new List<TableEntity>();
        var tableServiceClient = CreateTableServiceClient(
            capturedTrades,
            capturedStates,
            stateEntity: null);
        var trade = CreateTrade(DateTimeOffset.Parse("2026-07-09T10:15:00+00:00"), tradeId: 123);
        var source = new FakeTradeSource([trade]);
        var sut = CreateSut(tableServiceClient, source);

        var result = await sut.IngestAsync(CancellationToken.None);

        result.TradeCount.ShouldBe(1);
        capturedTrades.Count.ShouldBeGreaterThanOrEqualTo(1);
        capturedTrades[0].PartitionKey.ShouldBe("yolodaily|Hyperliquid|testnet|0xvault");
        capturedTrades[0].RowKey.ShouldBe("202607091015000000000|123");
        capturedTrades[0]["StrategyName"].ShouldBe("yolodaily");
        capturedTrades[0]["Exchange"].ShouldBe("Hyperliquid");
        capturedTrades[0]["ExchangeSymbol"].ShouldBe("BTC");
        capturedTrades[0]["ClientOrderId"].ShouldBe("client-123");
        capturedStates.Count.ShouldBe(1);
        capturedStates[0].PartitionKey.ShouldBe("yolodaily|Hyperliquid|testnet|0xvault");
        capturedStates[0].RowKey.ShouldBe("watermark");
    }

    [Fact]
    public async Task IngestAsync_WhenWatermarkExists_ShouldRestartFromOverlap()
    {
        var capturedTrades = new List<TableEntity>();
        var capturedStates = new List<TableEntity>();
        var stateEntity = new TableEntity("yolodaily|Hyperliquid|testnet|0xvault", "watermark")
        {
            ["LastSyncedThroughUtc"] = DateTimeOffset.Parse("2026-07-09T00:00:00+00:00")
        };
        var tableServiceClient = CreateTableServiceClient(capturedTrades, capturedStates, stateEntity);
        var source = new FakeTradeSource([]);
        var sut = CreateSut(
            tableServiceClient,
            source,
            new TradeIngestionOptions
            {
                StartUtc = DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
                WindowDays = 1,
                OverlapDays = 2
            });

        var result = await sut.IngestAsync(CancellationToken.None);

        result.StartUtc.ShouldBe(DateTimeOffset.Parse("2026-07-07T00:00:00+00:00"));
        source.Requests.ShouldNotBeEmpty();
        source.Requests[0].StartUtc.ShouldBe(DateTimeOffset.Parse("2026-07-07T00:00:00+00:00"));
    }

    private static AzureTableUserTradeIngestionService CreateSut(
        TableServiceClient tableServiceClient,
        IUserTradeSource source,
        TradeIngestionOptions? options = null)
    {
        var context = new UserTradeIngestionContext(
            "yolodaily",
            Exchange.Hyperliquid,
            "testnet",
            "0xwallet",
            "0xvault");

        return new AzureTableUserTradeIngestionService(
            context,
            options ?? new TradeIngestionOptions
            {
                StartUtc = DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
                WindowDays = 1,
                OverlapDays = 2
            },
            source,
            tableServiceClient,
            NullLogger<AzureTableUserTradeIngestionService>.Instance);
    }

    private static TableServiceClient CreateTableServiceClient(
        List<TableEntity> capturedTrades,
        List<TableEntity> capturedStates,
        TableEntity? stateEntity)
    {
        var tradesTable = CreateTableClient(capturedTrades);
        var stateTable = CreateTableClient(capturedStates);

        stateTable
            .Setup(x => x.GetEntityAsync<TableEntity>(
                "yolodaily|Hyperliquid|testnet|0xvault",
                "watermark",
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(stateEntity is null
                ? Task.FromException<Response<TableEntity>>(
                    new RequestFailedException(404, "not found"))
                : Task.FromResult(Response.FromValue(stateEntity, Mock.Of<Response>())));

        var tableServiceClient = new Mock<TableServiceClient>();
        tableServiceClient
            .Setup(x => x.GetTableClient("usertrades"))
            .Returns(tradesTable.Object);
        tableServiceClient
            .Setup(x => x.GetTableClient("usertradeingestionstate"))
            .Returns(stateTable.Object);

        return tableServiceClient.Object;
    }

    private static Mock<TableClient> CreateTableClient(List<TableEntity> capturedEntities)
    {
        var tableClient = new Mock<TableClient>();
        tableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Response.FromValue(new TableItem("table"), Mock.Of<Response>())));
        tableClient
            .Setup(x => x.UpsertEntityAsync(
                It.IsAny<TableEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<TableEntity, TableUpdateMode, CancellationToken>((entity, _, _) => capturedEntities.Add(entity))
            .ReturnsAsync(Mock.Of<Response>());

        return tableClient;
    }

    private static UserTradeRecord CreateTrade(DateTimeOffset timestampUtc, long tradeId) =>
        new(
            Exchange: Exchange.Hyperliquid,
            ClosedPnl: "0",
            ExchangeSymbol: "BTC",
            Symbol: "BTC",
            SymbolType: "Future",
            Crossed: true,
            Direction: "Open Long",
            Hash: "0xhash",
            OrderId: 456,
            Price: "100000",
            OrderSide: "Buy",
            StartPosition: "0",
            Quantity: "0.01",
            TimestampUtc: timestampUtc,
            Fee: "0.1",
            FeeToken: "USDC",
            BuilderFee: null,
            TradeId: tradeId,
            LiquidationJson: null,
            TwapId: null,
            ClientOrderId: $"client-{tradeId}",
            RawJson: "{}");

    private sealed class FakeTradeSource(IReadOnlyCollection<UserTradeRecord> trades) : IUserTradeSource
    {
        public List<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)> Requests { get; } = [];

        public Task<IReadOnlyCollection<UserTradeRecord>> GetUserTradesByTimeAsync(
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((startUtc, endUtc));
            return Task.FromResult(trades);
        }
    }
}
