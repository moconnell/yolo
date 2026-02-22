using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using YoloFunk.Dto;
using YoloFunk.Functions;

namespace YoloFunk.Test.Functions;

public class RebalanceDurableWorkflowTest
{
    [Fact]
    public async Task GivenNoExistingInstance_WhenStartIfNotRunningAsync_ShouldStartNewOrchestration()
    {
        // arrange
        var request = new RebalanceRequest(
            "test-strategy",
            "manual",
            DateTime.UtcNow);

        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);
        mockDurableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("rebalance-test-strategy");

        // act
        var (started, instanceId, existing) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            CancellationToken.None);

        // assert
        started.ShouldBeTrue();
        instanceId.ShouldBe("rebalance-test-strategy");
        existing.ShouldBeNull();
        mockDurableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                RebalanceDurableWorkflow.OrchestratorName,
                request,
                It.Is<StartOrchestrationOptions>(o => o.InstanceId == "rebalance-test-strategy"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenRunningInstance_WhenStartIfNotRunningAsync_ShouldNotStartNewOrchestration()
    {
        // arrange
        var request = new RebalanceRequest(
            "test-strategy",
            "manual",
            DateTime.UtcNow);

        var existingMetadata = new OrchestrationMetadata("rebalance-test-strategy", RebalanceDurableWorkflow.OrchestratorName);

        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMetadata);

        // act
        var (started, instanceId, existing) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            CancellationToken.None);

        // assert
        started.ShouldBeFalse();
        instanceId.ShouldBe("rebalance-test-strategy");
        existing.ShouldNotBeNull();
        existing!.IsRunning.ShouldBeTrue();
        mockDurableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenCompletedInstance_WhenStartIfNotRunningAsync_ShouldStartNewOrchestration()
    {
        // arrange
        var request = new RebalanceRequest(
            "test-strategy",
            "manual",
            DateTime.UtcNow);

        var completedMetadata = new OrchestrationMetadata("rebalance-test-strategy", RebalanceDurableWorkflow.OrchestratorName)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Completed,
        };

        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedMetadata);
        mockDurableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("rebalance-test-strategy");

        // act
        var (started, instanceId, existing) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            CancellationToken.None);

        // assert
        started.ShouldBeTrue();
        instanceId.ShouldBe("rebalance-test-strategy");
        existing.ShouldNotBeNull();
        existing!.IsRunning.ShouldBeFalse();
        mockDurableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                RebalanceDurableWorkflow.OrchestratorName,
                request,
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenCancellationToken_WhenStartIfNotRunningAsync_ShouldPassThroughToClient()
    {
        // arrange
        var request = new RebalanceRequest(
            "test-strategy",
            "manual",
            DateTime.UtcNow);
        var cts = new CancellationTokenSource();

        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                cts.Token))
            .ReturnsAsync((OrchestrationMetadata?)null);
        mockDurableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                cts.Token))
            .ReturnsAsync("rebalance-test-strategy");

        // act
        await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            cts.Token);

        // assert
        mockDurableClient.Verify(
            x => x.GetInstanceAsync(
                It.IsAny<string>(),
                false,
                cts.Token),
            Times.Once);
        mockDurableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public void GivenStrategyKey_WhenGetInstanceId_ShouldReturnFormattedId()
    {
        // act
        var instanceId = RebalanceDurableWorkflow.GetInstanceId("my-strategy");

        // assert
        instanceId.ShouldBe("rebalance-my-strategy");
    }

    [Fact]
    public async Task GivenMultipleCalls_WhenStartIfNotRunningAsync_ShouldUseConsistentInstanceId()
    {
        // arrange
        var request = new RebalanceRequest(
            "test-strategy",
            "manual",
            DateTime.UtcNow);

        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);
        mockDurableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("rebalance-test-strategy");

        // act
        var (_, instanceId1, _) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            CancellationToken.None);
        var (_, instanceId2, _) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            CancellationToken.None);

        // assert
        instanceId1.ShouldBe(instanceId2);
    }
}