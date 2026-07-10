using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;
using YoloFunk.Functions;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Functions;

public sealed class TradeIngestionDurableWorkflowTest
{
    [Fact]
    public async Task GivenRegisteredService_WhenRunTradeIngestionActivity_ShouldReturnSuccess()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        var ingestionResult = new UserTradeIngestionResult(
            "test-strategy",
            DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
            DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
            8,
            13);
        ingestionService
            .Setup(x => x.IngestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ingestionResult);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("test-strategy", ingestionService.Object);
        var sut = new TradeIngestionDurableWorkflow(
            services.BuildServiceProvider(),
            Mock.Of<ILogger<TradeIngestionDurableWorkflow>>());
        var request = new TradeIngestionRequest("test-strategy", "manual", DateTime.UtcNow);

        var result = await sut.RunTradeIngestionActivity(request, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.IngestionResult.ShouldBe(ingestionResult);
        result.ErrorMessage.ShouldBeNull();
        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenServiceThrows_WhenRunTradeIngestionActivity_ShouldReturnFailureWithMessage()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        ingestionService
            .Setup(x => x.IngestAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("activity failed"));

        var services = new ServiceCollection();
        services.AddKeyedSingleton("test-strategy", ingestionService.Object);
        var sut = new TradeIngestionDurableWorkflow(
            services.BuildServiceProvider(),
            Mock.Of<ILogger<TradeIngestionDurableWorkflow>>());
        var request = new TradeIngestionRequest("test-strategy", "manual", DateTime.UtcNow);

        var result = await sut.RunTradeIngestionActivity(request, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.IngestionResult.ShouldBeNull();
        result.ErrorMessage.ShouldBe("activity failed");
    }

    [Fact]
    public async Task GivenNoExistingInstance_WhenStartIfNotRunningAsync_ShouldStartNewOrchestration()
    {
        var request = new TradeIngestionRequest("test-strategy", "manual", DateTime.UtcNow);
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);
        durableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<TradeIngestionRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trade-ingestion-test-strategy");

        var (started, instanceId, existing) = await TradeIngestionDurableWorkflow.StartIfNotRunningAsync(
            durableClient.Object,
            request,
            CancellationToken.None);

        started.ShouldBeTrue();
        instanceId.ShouldBe("trade-ingestion-test-strategy");
        existing.ShouldBeNull();
        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                TradeIngestionDurableWorkflow.OrchestratorName,
                request,
                It.Is<StartOrchestrationOptions>(o => o.InstanceId == "trade-ingestion-test-strategy"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenRunningInstance_WhenStartIfNotRunningAsync_ShouldNotStartNewOrchestration()
    {
        var request = new TradeIngestionRequest("test-strategy", "manual", DateTime.UtcNow);
        var durableClient = new Mock<DurableTaskClient>("test-client");
        var existingMetadata = new OrchestrationMetadata(
            "trade-ingestion-test-strategy",
            TradeIngestionDurableWorkflow.OrchestratorName)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Running
        };
        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMetadata);

        var (started, instanceId, existing) = await TradeIngestionDurableWorkflow.StartIfNotRunningAsync(
            durableClient.Object,
            request,
            CancellationToken.None);

        started.ShouldBeFalse();
        instanceId.ShouldBe("trade-ingestion-test-strategy");
        existing.ShouldBe(existingMetadata);
        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<TradeIngestionRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Completed)]
    [InlineData(OrchestrationRuntimeStatus.Failed)]
    [InlineData(OrchestrationRuntimeStatus.Terminated)]
    public async Task GivenFinishedInstance_WhenStartIfNotRunningAsync_ShouldStartNewOrchestration(
        OrchestrationRuntimeStatus runtimeStatus)
    {
        var request = new TradeIngestionRequest("test-strategy", "manual", DateTime.UtcNow);
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationMetadata(
                "trade-ingestion-test-strategy",
                TradeIngestionDurableWorkflow.OrchestratorName)
            {
                RuntimeStatus = runtimeStatus
            });
        durableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<TradeIngestionRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trade-ingestion-test-strategy");

        var (started, instanceId, existing) = await TradeIngestionDurableWorkflow.StartIfNotRunningAsync(
            durableClient.Object,
            request,
            CancellationToken.None);

        started.ShouldBeTrue();
        instanceId.ShouldBe("trade-ingestion-test-strategy");
        existing.ShouldNotBeNull();
        existing!.RuntimeStatus.ShouldBe(runtimeStatus);
    }

    [Fact]
    public void GivenStrategyKey_WhenGetInstanceId_ShouldReturnFormattedId()
    {
        var instanceId = TradeIngestionDurableWorkflow.GetInstanceId("my-strategy");

        instanceId.ShouldBe("trade-ingestion-my-strategy");
    }
}
