using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YoloFunk.Functions;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Functions;

public sealed class TimerTradeIngestionFunctionBaseTest
{
    [Fact]
    public async Task GivenRegisteredService_WhenRun_ShouldIngest()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        ingestionService
            .Setup(x => x.IngestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTradeIngestionResult(
                "yolodaily",
                DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
                DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
                2,
                7));
        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var sut = new TestTimerTradeIngestion(services, NullLogger.Instance);

        await sut.Run(CancellationToken.None);

        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenMissingService_WhenRun_ShouldSkip()
    {
        var services = CreateServices();
        var sut = new TestTimerTradeIngestion(services, NullLogger.Instance);

        await sut.Run(CancellationToken.None);
    }

    [Fact]
    public async Task GivenRegisteredService_WhenYoloDailyRun_ShouldIngest()
    {
        var ingestionService = CreateIngestionService();
        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var sut = new YoloDailyTradeIngestion(
            services,
            NullLogger<YoloDailyTradeIngestion>.Instance);

        await sut.Run(new TimerInfo(), CancellationToken.None);

        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenRegisteredService_WhenUnravelDailyRun_ShouldIngest()
    {
        var ingestionService = CreateIngestionService();
        var services = CreateServices(services =>
            services.AddKeyedSingleton("unraveldaily", ingestionService.Object));
        var sut = new UnravelDailyTradeIngestion(
            services,
            NullLogger<UnravelDailyTradeIngestion>.Instance);

        await sut.Run(new TimerInfo(), CancellationToken.None);

        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Once);
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

    private sealed class TestTimerTradeIngestion(IServiceProvider serviceProvider, ILogger logger)
        : TimerTradeIngestionFunctionBase(serviceProvider, logger)
    {
        protected override string StrategyKey => "yolodaily";

        public Task Run(CancellationToken cancellationToken) => RunTradeIngestionAsync(cancellationToken);
    }
}
