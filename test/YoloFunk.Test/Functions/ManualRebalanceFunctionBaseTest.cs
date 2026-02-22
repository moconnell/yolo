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

namespace YoloFunk.Test.Functions;

public class ManualRebalanceFunctionBaseTest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GivenNoExistingInstance_WhenYoloRun_ShouldReturnAcceptedAndScheduledPayload()
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
                RebalanceDurableWorkflow.OrchestratorName,
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("rebalance-yolodaily");

        var request = TestHttpRequestData.Create("POST");
        var sut = new YoloDailyManualRebalance(Mock.Of<ILogger<YoloDailyManualRebalance>>());

        var response = await sut.Run(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceStartResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
        payload.InstanceId.ShouldBe("rebalance-yolodaily");
        payload.Started.ShouldBeTrue();
        payload.RuntimeStatus.ShouldBe("Scheduled");
    }

    [Fact]
    public async Task GivenExistingRunningInstance_WhenUnravelRun_ShouldReturnOkAndRunningPayload()
    {
        var durableClient = new Mock<DurableTaskClient>("test-client");
        var existingMetadata = new OrchestrationMetadata("rebalance-unraveldaily", RebalanceDurableWorkflow.OrchestratorName)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Running,
        };

        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMetadata);

        var request = TestHttpRequestData.Create("POST");
        var sut = new UnravelDailyManualRebalance(Mock.Of<ILogger<UnravelDailyManualRebalance>>());

        var response = await sut.Run(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceStartResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("unraveldaily");
        payload.InstanceId.ShouldBe("rebalance-unraveldaily");
        payload.Started.ShouldBeFalse();
        payload.RuntimeStatus.ShouldBe(OrchestrationRuntimeStatus.Running.ToString());

        durableClient.Verify(
            x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<RebalanceRequest>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GivenNoStatusInstance_WhenYoloStatus_ShouldReturnNotFound()
    {
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                "rebalance-yolodaily",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);

        var request = TestHttpRequestData.Create("GET");
        var sut = new YoloDailyManualRebalance(Mock.Of<ILogger<YoloDailyManualRebalance>>());

        var response = await sut.Status(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
        payload.Error.ShouldBe("No orchestration found");
    }

    [Fact]
    public async Task GivenExistingStatus_WhenUnravelStatus_ShouldReturnOkAndStatusPayload()
    {
        var durableClient = new Mock<DurableTaskClient>("test-client");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var updatedAt = DateTimeOffset.UtcNow;
        var metadata = new OrchestrationMetadata("rebalance-unraveldaily", RebalanceDurableWorkflow.OrchestratorName)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Completed,
            CreatedAt = createdAt,
            LastUpdatedAt = updatedAt,
        };

        durableClient
            .Setup(x => x.GetInstanceAsync(
                "rebalance-unraveldaily",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var request = TestHttpRequestData.Create("GET");
        var sut = new UnravelDailyManualRebalance(Mock.Of<ILogger<UnravelDailyManualRebalance>>());

        var response = await sut.Status(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceStatusResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("unraveldaily");
        payload.InstanceId.ShouldBe("rebalance-unraveldaily");
        payload.RuntimeStatus.ShouldBe(OrchestrationRuntimeStatus.Completed.ToString());
        payload.CreatedAt.ShouldBe(createdAt);
        payload.LastUpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public async Task GivenClientThrows_WhenRunManual_ShouldReturnInternalServerError()
    {
        var durableClient = new Mock<DurableTaskClient>("test-client");
        durableClient
            .Setup(x => x.GetInstanceAsync(
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var request = TestHttpRequestData.Create("POST");
        var sut = new ManualRebalanceFunctionHarness(Mock.Of<ILogger<ManualRebalanceFunctionHarness>>());

        var response = await sut.Run(request, durableClient.Object, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("test");
        payload.Error.ShouldBe("Failed to start rebalance");
        payload.Details.ShouldBe("An internal error occurred. Check logs for details.");
    }

    [Theory]
    [InlineData(null, HttpStatusCode.NotFound)]
    [InlineData(OrchestrationRuntimeStatus.Pending, HttpStatusCode.OK)]
    [InlineData(OrchestrationRuntimeStatus.Running, HttpStatusCode.OK)]
    [InlineData(OrchestrationRuntimeStatus.Completed, HttpStatusCode.OK)]
    [InlineData(OrchestrationRuntimeStatus.Failed, HttpStatusCode.InternalServerError)]
    [InlineData(OrchestrationRuntimeStatus.Terminated, HttpStatusCode.Conflict)]
    public void GivenRuntimeStatus_WhenResolveStatusCodeForExisting_ShouldReturnExpected(
        OrchestrationRuntimeStatus? runtimeStatus,
        HttpStatusCode expectedStatusCode)
    {
        var sut = new ManualRebalanceFunctionHarness(Mock.Of<ILogger<ManualRebalanceFunctionHarness>>());

        var statusCode = sut.ResolveStatus(runtimeStatus);

        statusCode.ShouldBe(expectedStatusCode);
    }

    public sealed class ManualRebalanceFunctionHarness(ILogger<ManualRebalanceFunctionHarness> logger)
        : ManualRebalanceFunctionBase(logger)
    {
        protected override string StrategyKey => "test";

        public Task<HttpResponseData> Run(HttpRequestData req, DurableTaskClient durableClient, CancellationToken cancellationToken)
            => RunManualRebalanceAsync(req, durableClient, cancellationToken);

        public HttpStatusCode ResolveStatus(OrchestrationRuntimeStatus? runtimeStatus)
            => ResolveStatusCodeForExisting(runtimeStatus);
    }

    private sealed class TestHttpRequestData : HttpRequestData
    {
        private readonly string _method;

        private TestHttpRequestData(FunctionContext functionContext)
            : base(functionContext)
        {
            _method = "POST";
        }

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

        public static TestHttpRequestData Create(string method)
        {
            var functionContext = new Mock<FunctionContext>();
            functionContext
                .SetupGet(x => x.InstanceServices)
                .Returns(CreateInstanceServices());

            return new TestHttpRequestData(functionContext.Object, method);
        }

        private static IServiceProvider CreateInstanceServices()
        {
            var services = new ServiceCollection();
            services.AddOptions<WorkerOptions>()
                .Configure(options => options.Serializer = new JsonObjectSerializer());
            return services.BuildServiceProvider();
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
