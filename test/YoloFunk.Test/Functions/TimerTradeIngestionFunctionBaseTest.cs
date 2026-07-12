using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YoloFunk.Dto;
using YoloFunk.Functions;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Functions;

public sealed class TimerTradeIngestionFunctionBaseTest
{
    [Fact]
    public async Task GivenRegisteredService_WhenRun_ShouldStartDurableIngestion()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        var durableClient = CreateDurableClient();
        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var sut = new TestTimerTradeIngestion(services, NullLogger.Instance);

        await sut.Run(durableClient.Object, CancellationToken.None);

        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                TradeIngestionDurableWorkflow.OrchestratorName,
                It.Is<TradeIngestionRequest>(r => r.StrategyKey == "yolodaily" && r.Trigger == "timer"),
                It.Is<StartOrchestrationOptions>(o => o.InstanceId == "trade-ingestion-yolodaily"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenMissingService_WhenRun_ShouldSkip()
    {
        var services = CreateServices();
        var sut = new TestTimerTradeIngestion(services, NullLogger.Instance);
        var durableClient = CreateDurableClient();

        await sut.Run(durableClient.Object, CancellationToken.None);

        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<TradeIngestionRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenRegisteredService_WhenYoloDailyScheduledRun_ShouldStartDurableIngestion()
    {
        var ingestionService = CreateIngestionService();
        var durableClient = CreateDurableClient();
        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var sut = new YoloDailyScheduledTradeIngestion(
            services,
            NullLogger<YoloDailyScheduledTradeIngestion>.Instance);

        await sut.Run(new TimerInfo(), durableClient.Object, CancellationToken.None);

        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                TradeIngestionDurableWorkflow.OrchestratorName,
                It.Is<TradeIngestionRequest>(r => r.StrategyKey == "yolodaily"),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenRegisteredService_WhenUnravelDailyScheduledRun_ShouldStartDurableIngestion()
    {
        var ingestionService = CreateIngestionService();
        var durableClient = CreateDurableClient();
        var services = CreateServices(services =>
            services.AddKeyedSingleton("unraveldaily", ingestionService.Object));
        var sut = new UnravelDailyScheduledTradeIngestion(
            services,
            NullLogger<UnravelDailyScheduledTradeIngestion>.Instance);

        await sut.Run(new TimerInfo(), durableClient.Object, CancellationToken.None);

        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                TradeIngestionDurableWorkflow.OrchestratorName,
                It.Is<TradeIngestionRequest>(r => r.StrategyKey == "unraveldaily"),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IServiceProvider CreateServices(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static Mock<IUserTradeIngestionService> CreateIngestionService()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        ingestionService
            .Setup(x => x.IngestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTradeIngestionResult(
                "strategy",
                DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
                DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
                1,
                1));

        return ingestionService;
    }

    private static Mock<DurableTaskClient> CreateDurableClient()
    {
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);
        durableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<TradeIngestionRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskName _, TradeIngestionRequest request, StartOrchestrationOptions options, CancellationToken _) =>
                options.InstanceId ?? TradeIngestionDurableWorkflow.GetInstanceId(request.StrategyKey));

        return durableClient;
    }

    private sealed class TestTimerTradeIngestion(IServiceProvider serviceProvider, ILogger logger)
        : TimerTradeIngestionFunctionBase(serviceProvider, logger)
    {
        protected override string StrategyKey => "yolodaily";

        public Task Run(DurableTaskClient durableClient, CancellationToken cancellationToken) =>
            RunTradeIngestionAsync(durableClient, cancellationToken);
    }
}
