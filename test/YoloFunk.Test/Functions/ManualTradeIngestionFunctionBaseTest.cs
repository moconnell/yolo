using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
    public async Task GivenRegisteredService_WhenYoloRun_ShouldReturnOkAndIngestionResult()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        ingestionService
            .Setup(x => x.IngestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTradeIngestionResult(
                "yolodaily",
                DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
                DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
                8,
                13));

        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var request = TestHttpRequestData.Create("POST", services);
        var sut = new YoloDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<YoloDailyManualTradeIngestion>>());

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<UserTradeIngestionResult>(response);
        payload.ShouldNotBeNull();
        payload.StrategyName.ShouldBe("yolodaily");
        payload.WindowCount.ShouldBe(8);
        payload.TradeCount.ShouldBe(13);
    }

    [Fact]
    public async Task GivenMissingService_WhenUnravelRun_ShouldReturnServiceUnavailable()
    {
        var services = CreateServices();
        var request = TestHttpRequestData.Create("POST", services);
        var sut = new UnravelDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<UnravelDailyManualTradeIngestion>>());

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("unraveldaily");
        payload.Error.ShouldBe("Trade ingestion is not configured");
    }

    [Fact]
    public async Task GivenServiceThrows_WhenRun_ShouldReturnInternalServerError()
    {
        var ingestionService = new Mock<IUserTradeIngestionService>();
        ingestionService
            .Setup(x => x.IngestAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var services = CreateServices(services =>
            services.AddKeyedSingleton("yolodaily", ingestionService.Object));
        var request = TestHttpRequestData.Create("POST", services);
        var sut = new YoloDailyManualTradeIngestion(
            services,
            Mock.Of<ILogger<YoloDailyManualTradeIngestion>>());

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
        payload.Error.ShouldBe("Failed to run trade ingestion");
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
