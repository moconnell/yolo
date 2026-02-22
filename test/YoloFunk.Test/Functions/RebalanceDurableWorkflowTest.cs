using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloAbstractions.Interfaces;
using YoloFunk.Dto;
using YoloFunk.Functions;

namespace YoloFunk.Test.Functions;

public class RebalanceDurableWorkflowTest
{
    [Fact]
    public async Task GivenRegisteredCommand_WhenRunRebalanceActivity_ShouldReturnSuccess()
    {
        var command = new Mock<ICommand>();
        command
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("test-strategy", command.Object);

        var sut = new RebalanceDurableWorkflow(
            services.BuildServiceProvider(),
            Mock.Of<ILogger<RebalanceDurableWorkflow>>());

        var request = new RebalanceRequest("test-strategy", "manual", DateTime.UtcNow);

        var result = await sut.RunRebalanceActivity(request, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        command.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenCommandThrows_WhenRunRebalanceActivity_ShouldReturnFailureWithMessage()
    {
        var command = new Mock<ICommand>();
        command
            .Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("activity failed"));

        var services = new ServiceCollection();
        services.AddKeyedSingleton("test-strategy", command.Object);

        var sut = new RebalanceDurableWorkflow(
            services.BuildServiceProvider(),
            Mock.Of<ILogger<RebalanceDurableWorkflow>>());

        var request = new RebalanceRequest("test-strategy", "manual", DateTime.UtcNow);

        var result = await sut.RunRebalanceActivity(request, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("activity failed");
        command.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

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
    public async Task GivenFailedInstance_WhenStartIfNotRunningAsync_ShouldStartNewOrchestration()
    {
        var request = new RebalanceRequest(
            "test-strategy",
            "manual",
            DateTime.UtcNow);

        var failedMetadata = new OrchestrationMetadata("rebalance-test-strategy", RebalanceDurableWorkflow.OrchestratorName)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Failed,
        };

        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedMetadata);
        mockDurableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("rebalance-test-strategy");

        var (started, instanceId, existing) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            CancellationToken.None);

        started.ShouldBeTrue();
        instanceId.ShouldBe("rebalance-test-strategy");
        existing.ShouldNotBeNull();
        existing!.RuntimeStatus.ShouldBe(OrchestrationRuntimeStatus.Failed);
    }

    [Fact]
    public async Task GivenTerminatedInstance_WhenStartIfNotRunningAsync_ShouldStartNewOrchestration()
    {
        var request = new RebalanceRequest(
            "test-strategy",
            "manual",
            DateTime.UtcNow);

        var terminatedMetadata = new OrchestrationMetadata("rebalance-test-strategy", RebalanceDurableWorkflow.OrchestratorName)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Terminated,
        };

        var mockDurableClient = new Mock<DurableTaskClient>("test-client");
        mockDurableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(terminatedMetadata);
        mockDurableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("rebalance-test-strategy");

        var (started, instanceId, existing) = await RebalanceDurableWorkflow.StartIfNotRunningAsync(
            mockDurableClient.Object,
            request,
            CancellationToken.None);

        started.ShouldBeTrue();
        instanceId.ShouldBe("rebalance-test-strategy");
        existing.ShouldNotBeNull();
        existing!.RuntimeStatus.ShouldBe(OrchestrationRuntimeStatus.Terminated);
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