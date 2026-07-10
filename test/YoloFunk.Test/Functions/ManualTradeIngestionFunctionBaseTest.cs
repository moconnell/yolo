using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoloFunk.Dto;
using YoloFunk.Functions;
using YoloFunk.Infrastructure;

namespace YoloFunk.Test.Functions;

public sealed class ManualTradeIngestionFunctionBaseTest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GivenRegisteredService_WhenYoloRun_ShouldReturnAcceptedAndScheduledPayload()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);
        durableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                TradeIngestionDurableWorkflow.OrchestratorName,
                It.IsAny<TradeIngestionRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("trade-ingestion-yolodaily");
        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var request = TestHttpRequestData.Create("POST", services);
        var sut = new YoloDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<YoloDailyManualTradeIngestion>>());

        var response = await sut.Run(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var payload = await TestHttpRequestData.ReadJsonAsync<TradeIngestionStartResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
        payload.InstanceId.ShouldBe("trade-ingestion-yolodaily");
        payload.Started.ShouldBeTrue();
        payload.RuntimeStatus.ShouldBe("Scheduled");

        ingestionService.Verify(x => x.IngestAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenMissingService_WhenUnravelRun_ShouldReturnServiceUnavailable()
    {
        var services = CreateServices();
        var request = TestHttpRequestData.Create("POST", services);
        var durableClient = new Mock<DurableTaskClient>("test-client");
        var sut = new UnravelDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<UnravelDailyManualTradeIngestion>>());

        var response = await sut.Run(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("unraveldaily");
        payload.Error.ShouldBe("Trade ingestion is not configured");
    }

    [Fact]
    public async Task GivenExistingRunningInstance_WhenRun_ShouldReturnOkAndRunningPayload()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                "trade-ingestion-yolodaily",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationMetadata(
                "trade-ingestion-yolodaily",
                TradeIngestionDurableWorkflow.OrchestratorName)
            {
                RuntimeStatus = OrchestrationRuntimeStatus.Running
            });
        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var request = TestHttpRequestData.Create("POST", services);
        var sut = new YoloDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<YoloDailyManualTradeIngestion>>());

        var response = await sut.Run(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<TradeIngestionStartResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
        payload.Started.ShouldBeFalse();
        payload.RuntimeStatus.ShouldBe(OrchestrationRuntimeStatus.Running.ToString());

        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<TradeIngestionRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenClientThrows_WhenRun_ShouldReturnInternalServerError()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var request = TestHttpRequestData.Create("POST", services);
        var sut = new YoloDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<YoloDailyManualTradeIngestion>>());

        var response = await sut.Run(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
        payload.Error.ShouldBe("Failed to start trade ingestion");
    }

    [Fact]
    public async Task GivenExistingStatus_WhenStatus_ShouldReturnOkAndStatusPayload()
    {
        var services = CreateServices();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var updatedAt = DateTimeOffset.UtcNow;
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                "trade-ingestion-unraveldaily",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationMetadata(
                "trade-ingestion-unraveldaily",
                TradeIngestionDurableWorkflow.OrchestratorName)
            {
                RuntimeStatus = OrchestrationRuntimeStatus.Completed,
                CreatedAt = createdAt,
                LastUpdatedAt = updatedAt
            });
        var request = TestHttpRequestData.Create("GET", services);
        var sut = new UnravelDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<UnravelDailyManualTradeIngestion>>());

        var response = await sut.Status(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<TradeIngestionStatusResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("unraveldaily");
        payload.InstanceId.ShouldBe("trade-ingestion-unraveldaily");
        payload.RuntimeStatus.ShouldBe(OrchestrationRuntimeStatus.Completed.ToString());
        payload.CreatedAt.ShouldBe(createdAt);
        payload.LastUpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public async Task GivenNoStatusInstance_WhenStatus_ShouldReturnNotFound()
    {
        var services = CreateServices();
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                "trade-ingestion-yolodaily",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);
        var request = TestHttpRequestData.Create("GET", services);
        var sut = new YoloDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<YoloDailyManualTradeIngestion>>());

        var response = await sut.Status(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
        payload.Error.ShouldBe("No orchestration found");
    }

    private static IServiceProvider CreateServices(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddOptions<WorkerOptions>()
            .Configure(options => options.Serializer = new JsonObjectSerializer());
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private sealed class TestHttpRequestData : HttpRequestData
    {
        private readonly string _method;

        private TestHttpRequestData(FunctionContext functionContext, string method)
            : base(functionContext)
        {
            _method = method;
        }

        public override Stream Body { get; } = new MemoryStream();

        public override HttpHeadersCollection Headers { get; } = [];

        public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];

        public override Uri Url { get; } = new("http://localhost");

        public override IEnumerable<ClaimsIdentity> Identities { get; } = [];

        public override string Method => _method;

        public override HttpResponseData CreateResponse()
            => new TestHttpResponseData(FunctionContext);

        public static TestHttpRequestData Create(string method, IServiceProvider services)
        {
            var functionContext = new Mock<FunctionContext>();
            functionContext
                .SetupGet(x => x.InstanceServices)
                .Returns(services);

            return new TestHttpRequestData(functionContext.Object, method);
        }

        public static async Task<T?> ReadJsonAsync<T>(HttpResponseData response)
        {
            response.Body.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(response.Body, JsonOptions);
        }
    }

    private sealed class TestHttpResponseData(FunctionContext functionContext) : HttpResponseData(functionContext)
    {
        public override HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        public override HttpHeadersCollection Headers { get; set; } = [];

        public override Stream Body { get; set; } = new MemoryStream();

        public override HttpCookies Cookies { get; } = new TestHttpCookies();
    }

    private sealed class TestHttpCookies : HttpCookies
    {
        private readonly List<IHttpCookie> _cookies = [];

        public override void Append(string name, string value)
            => _cookies.Add(new TestHttpCookie(name, value));

        public override void Append(IHttpCookie cookie)
            => _cookies.Add(cookie);

        public override IHttpCookie CreateNew()
            => new TestHttpCookie(string.Empty, string.Empty);
    }

    private sealed class TestHttpCookie(string name, string value) : IHttpCookie
    {
        public string Name { get; } = name;
        public string Value { get; } = value;
        public DateTimeOffset? Expires { get; set; }
        public bool? HttpOnly { get; set; }
        public double? MaxAge { get; set; }
        public string? Domain { get; set; }
        public string? Path { get; set; }
        public SameSite SameSite { get; set; } = SameSite.None;
        public bool? Secure { get; set; }
    }
}
