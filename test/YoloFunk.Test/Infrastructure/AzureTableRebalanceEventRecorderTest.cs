using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using YoloAbstractions;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Infrastructure;

public class AzureTableRebalanceEventRecorderTest
{
    [Fact]
    public async Task RecordAsync_WhenRecordHasValues_ShouldAddAppendOnlyEventEntity()
    {
        TableEntity? capturedEntity = null;
        var tableClient = new Mock<TableClient>();
        tableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Response.FromValue(new TableItem("rebalanceevents"), Mock.Of<Response>())));
        tableClient
            .Setup(x => x.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()))
            .Callback<TableEntity, CancellationToken>((entity, _) => capturedEntity = entity)
            .ReturnsAsync(Mock.Of<Response>());

        var tableServiceClient = new Mock<TableServiceClient>();
        tableServiceClient
            .Setup(x => x.GetTableClient("rebalanceevents"))
            .Returns(tableClient.Object);

        var record = new RebalanceEventRecord
        {
            RunId = "run/1",
            StrategyName = "yolo/daily",
            TimestampUtc = DateTimeOffset.Parse("2026-06-28T10:00:00+00:00"),
            Sequence = 7,
            EventType = "TradeProposed",
            Level = "Info",
            Summary = "Buy BTC",
            WalletAddress = "0xwallet",
            VaultAddress = "0xvault",
            Coin = "BTC",
            ClientOrderId = "client/1",
            OrderId = "123",
            PayloadJson = "{\"symbol\":\"BTC\"}"
        };
        var sut = new AzureTableRebalanceEventRecorder(tableServiceClient.Object);

        await sut.RecordAsync(record, CancellationToken.None);

        capturedEntity.ShouldNotBeNull();
        capturedEntity.PartitionKey.ShouldBe("yolo|daily|run|1");
        capturedEntity.RowKey.ShouldBe("202606281000000000000|000007|TradeProposed");
        capturedEntity["RunId"].ShouldBe("run/1");
        capturedEntity["StrategyName"].ShouldBe("yolo/daily");
        capturedEntity["EventType"].ShouldBe("TradeProposed");
        capturedEntity["Coin"].ShouldBe("BTC");
        capturedEntity["ClientOrderId"].ShouldBe("client/1");
        capturedEntity["PayloadJson"].ShouldBe("{\"symbol\":\"BTC\"}");
    }
}
