using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Azure;
using Azure.Core.Serialization;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using YoloFunk.Dto;
using YoloFunk.Functions;

namespace YoloFunk.Test.Functions;

public class StorageQueryFunctionsTest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetTradeExecutions_WhenTableClientMissing_ShouldReturnServiceUnavailable()
    {
        var request = TestHttpRequestData.Create("GET", "http://localhost/api/storage/trade-executions");
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetTradeExecutions(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetHttpRequestPayload_WhenBlobClientConfiguredButBlobNameMissing_ShouldReturnBadRequest()
    {
        var blobServiceClient = new Mock<BlobServiceClient>();
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/http-requests/payload",
            services => services.AddSingleton(blobServiceClient.Object));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetHttpRequestPayload(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTradeExecutions_WhenTableHasRows_ShouldReturnFilteredPageWithContinuationToken()
    {
        var tableServiceClient = CreateTableServiceClient(
            "tradeexecutions",
            [
                new TableEntity("yolodaily", "run-1|exec-1")
                {
                    ["ExecutionId"] = "exec-1",
                    ["RunId"] = "run-1",
                    ["StrategyName"] = "yolodaily",
                    ["Coin"] = "BTC",
                    ["Side"] = "Buy",
                    ["OrderType"] = "Limit",
                    ["SubmittedAt"] = DateTimeOffset.Parse("2026-06-28T10:00:00+00:00"),
                    ["RecordedAt"] = DateTimeOffset.Parse("2026-06-28T10:00:01+00:00"),
                    ["Status"] = "Filled",
                    ["FilledQty"] = "1.25"
                },
                new TableEntity("yolodaily", "run-1|exec-2")
                {
                    ["ExecutionId"] = "exec-2",
                    ["RunId"] = "run-1",
                    ["StrategyName"] = "yolodaily",
                    ["Coin"] = "ETH",
                    ["Side"] = "Sell",
                    ["OrderType"] = "Market",
                    ["SubmittedAt"] = DateTimeOffset.Parse("2026-06-28T10:01:00+00:00"),
                    ["RecordedAt"] = DateTimeOffset.Parse("2026-06-28T10:01:01+00:00"),
                    ["Status"] = "Created"
                }
            ],
            continuationToken: "next-page");
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/trade-executions?strategy=yolodaily&coin=BTC&pageSize=2&from=2026-06-28T09:00:00%2B00:00",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetTradeExecutions(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<TradeExecutionQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.PageSize.ShouldBe(2);
        payload.NextContinuationToken.ShouldBe("next-page");
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].ExecutionId.ShouldBe("exec-1");
        payload.Items[0].Coin.ShouldBe("BTC");
        payload.Items[0].FilledQty.ShouldBe("1.25");
    }

    [Fact]
    public async Task GetTradeExecutions_WhenFromDateIsAmbiguous_ShouldIgnoreDateFilter()
    {
        var tableServiceClient = CreateTableServiceClient(
            "tradeexecutions",
            [
                new TableEntity("yolodaily", "run-1|exec-1")
                {
                    ["ExecutionId"] = "exec-1",
                    ["RunId"] = "run-1",
                    ["StrategyName"] = "yolodaily",
                    ["Coin"] = "BTC",
                    ["Side"] = "Buy",
                    ["OrderType"] = "Limit",
                    ["SubmittedAt"] = DateTimeOffset.Parse("2026-06-28T10:00:00+00:00"),
                    ["RecordedAt"] = DateTimeOffset.Parse("2026-06-28T10:00:01+00:00")
                }
            ]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/trade-executions?strategy=yolodaily&from=06/28/2026",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetTradeExecutions(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<TradeExecutionQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetHttpRequestCaptures_WhenTableHasRows_ShouldReturnFilteredPage()
    {
        var tableServiceClient = CreateTableServiceClient(
            "httprequestsindex",
            [
                new TableEntity("api.robotwealth.com", "weights|hash-1")
                {
                    ["Host"] = "api.robotwealth.com",
                    ["Endpoint"] = "/v1/yolo/weights",
                    ["Url"] = "https://api.robotwealth.com/v1/yolo/weights",
                    ["Method"] = "GET",
                    ["StatusCode"] = 200,
                    ["BlobContainer"] = "http-requests",
                    ["BlobName"] = "api.robotwealth.com/v1/yolo/weights/hash-1.json",
                    ["ContentHash"] = "hash-1",
                    ["RequestTimeUtc"] = DateTimeOffset.Parse("2026-06-28T10:00:00+00:00"),
                    ["QueryParametersJson"] = "{}"
                }
            ]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/http-requests?host=api.robotwealth.com&method=GET&statusCode=200&from=2026-06-28T09:00:00%2B00:00",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetHttpRequestCaptures(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<HttpRequestCaptureQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].Host.ShouldBe("api.robotwealth.com");
        payload.Items[0].ContentHash.ShouldBe("hash-1");
    }

    private static TableServiceClient CreateTableServiceClient(
        string tableName,
        IReadOnlyList<TableEntity> entities,
        string? continuationToken = null)
    {
        var page = Page<TableEntity>.FromValues(entities, continuationToken, Mock.Of<Response>());
        var tableClient = new Mock<TableClient>();
        tableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages([page]));

        var tableServiceClient = new Mock<TableServiceClient>();
        tableServiceClient
            .Setup(x => x.GetTableClient(tableName))
            .Returns(tableClient.Object);

        return tableServiceClient.Object;
    }

    private sealed class TestHttpRequestData : HttpRequestData
    {
        private readonly string _method;
        private readonly Uri _url;

        private TestHttpRequestData(FunctionContext functionContext, string method, string url)
            : base(functionContext)
        {
            _method = method;
            _url = new Uri(url);
        }

        public override Stream Body { get; } = new MemoryStream();

        public override HttpHeadersCollection Headers { get; } = [];

        public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];

        public override Uri Url => _url;

        public override IEnumerable<ClaimsIdentity> Identities { get; } = [];

        public override string Method => _method;

        public override HttpResponseData CreateResponse()
            => new TestHttpResponseData(FunctionContext);

        public static TestHttpRequestData Create(
            string method,
            string url,
            Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddOptions<WorkerOptions>()
                .Configure(options => options.Serializer = new JsonObjectSerializer());
            configureServices?.Invoke(services);

            var functionContext = new Mock<FunctionContext>();
            functionContext
                .SetupGet(x => x.InstanceServices)
                .Returns(services.BuildServiceProvider());

            return new TestHttpRequestData(functionContext.Object, method, url);
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
