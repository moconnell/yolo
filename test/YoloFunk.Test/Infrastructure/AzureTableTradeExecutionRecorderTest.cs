using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using YoloAbstractions;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Infrastructure;

public class AzureTableTradeExecutionRecorderTest
{
    [Fact]
    public async Task RecordAsync_WhenRecordHasValues_ShouldUpsertSanitizedEntity()
    {
        TableEntity? capturedEntity = null;
        var tableClient = new Mock<TableClient>();
        tableClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Response.FromValue(new TableItem("tradeexecutions"), Mock.Of<Response>())));
        tableClient
            .Setup(x => x.UpsertEntityAsync(
                It.IsAny<TableEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<TableEntity, TableUpdateMode, CancellationToken>((entity, _, _) => capturedEntity = entity)
            .ReturnsAsync(Mock.Of<Response>());

        var tableServiceClient = new Mock<TableServiceClient>();
        tableServiceClient
            .Setup(x => x.GetTableClient("tradeexecutions"))
            .Returns(tableClient.Object);

        var record = new TradeExecutionRecord
        {
            ExecutionId = "exec/1",
            RunId = "run?1",
            StrategyName = "yolo/daily",
            WalletAddress = "0xwallet",
            VaultAddress = "0xvault",
            Coin = "BTC",
            Side = "Buy",
            TargetPosition = 2.5m,
            CurrentPosition = 1.25m,
            IntendedDelta = 1.25m,
            ArrivalMid = 100m,
            ArrivalBid = 99m,
            ArrivalAsk = 101m,
            SpreadBps = 200m,
            OrderId = "123",
            OrderType = "Limit",
            PostOnly = true,
            ReduceOnly = false,
            LimitPrice = 100.5m,
            SubmittedAt = DateTimeOffset.Parse("2026-06-28T10:00:00+00:00"),
            FilledQty = 1.25m,
            AvgFillPrice = 100.25m,
            Fees = 0.01m,
            MakerQty = 1m,
            MakerAvgFillPrice = 100m,
            MakerFees = 0.005m,
            TakerQty = 0.25m,
            TakerAvgFillPrice = 101m,
            TakerFees = 0.005m,
            CancelledQty = 0m,
            CompletedAt = DateTimeOffset.Parse("2026-06-28T10:01:00+00:00"),
            Status = "Filled",
            Error = "none"
        };
        var sut = new AzureTableTradeExecutionRecorder(tableServiceClient.Object);

        await sut.RecordAsync(record, CancellationToken.None);

        capturedEntity.ShouldNotBeNull();
        capturedEntity.PartitionKey.ShouldBe("yolo|daily");
        capturedEntity.RowKey.ShouldBe("run_1|exec|1");
        capturedEntity["ExecutionId"].ShouldBe("exec/1");
        capturedEntity["WalletAddress"].ShouldBe("0xwallet");
        capturedEntity["TargetPosition"].ShouldBe("2.5");
        capturedEntity["PostOnly"].ShouldBe(true);
        capturedEntity["ReduceOnly"].ShouldBe(false);
        capturedEntity["Status"].ShouldBe("Filled");
        capturedEntity["Error"].ShouldBe("none");
        capturedEntity.ContainsKey("RecordedAt").ShouldBeTrue();

        tableClient.Verify(
            x => x.UpsertEntityAsync(
                It.IsAny<TableEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
