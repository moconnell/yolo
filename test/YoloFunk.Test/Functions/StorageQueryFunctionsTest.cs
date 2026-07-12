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

    [Fact]
    public async Task GetHttpRequestCaptures_WhenRawPagesContainNonMatches_ShouldFillFilteredPage()
    {
        var tableServiceClient = CreateTableServiceClient(
            "httprequestsindex",
            [
                CreateHttpRequestEntity("api.robotwealth.com", "/v1/yolo/weights", "hash-1"),
                CreateHttpRequestEntity("api.robotwealth.com", "/v1/yolo/factors", "hash-2"),
                CreateHttpRequestEntity("api.robotwealth.com", "/v1/yolo/weights", "hash-3"),
                CreateHttpRequestEntity("api.robotwealth.com", "/v1/yolo/weights", "hash-4")
            ],
            continuationTokens: ["token-1", "token-2", "token-3", "token-4"]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/http-requests?endpoint=weights&pageSize=2",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetHttpRequestCaptures(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<HttpRequestCaptureQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Select(item => item.ContentHash).ShouldBe(["hash-1", "hash-3"]);
        payload.TotalCount.ShouldBe(2);
        payload.NextContinuationToken.ShouldBe("token-3");
    }

    [Fact]
    public async Task GetRebalanceEvents_WhenTableHasRows_ShouldReturnFilteredEvents()
    {
        var tableServiceClient = CreateTableServiceClient(
            "rebalanceevents",
            [
                new TableEntity("yolodaily|run-1", "202606281000000000000|000001|RunStarted")
                {
                    ["RunId"] = "run-1",
                    ["StrategyName"] = "yolodaily",
                    ["TimestampUtc"] = DateTimeOffset.Parse("2026-06-28T10:00:00+00:00"),
                    ["Sequence"] = 1,
                    ["EventType"] = "RunStarted",
                    ["Level"] = "Info",
                    ["Summary"] = "Rebalance run started",
                    ["WalletAddress"] = "0xwallet",
                    ["Coin"] = "BTC",
                    ["PayloadJson"] = "{\"baseAsset\":\"USDC\"}"
                },
                new TableEntity("yolodaily|run-1", "202606281001000000000|000002|TradeProposed")
                {
                    ["RunId"] = "run-1",
                    ["StrategyName"] = "yolodaily",
                    ["TimestampUtc"] = DateTimeOffset.Parse("2026-06-28T10:01:00+00:00"),
                    ["Sequence"] = 2,
                    ["EventType"] = "TradeProposed",
                    ["Level"] = "Info",
                    ["Summary"] = "Buy ETH",
                    ["Coin"] = "ETH",
                    ["ClientOrderId"] = "client-1",
                    ["PayloadJson"] = "{\"symbol\":\"ETH\"}"
                }
            ]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/rebalance-events?strategy=yolodaily&runId=run-1&eventType=TradeProposed",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetRebalanceEvents(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<RebalanceEventQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].EventType.ShouldBe("TradeProposed");
        payload.Items[0].ClientOrderId.ShouldBe("client-1");
        payload.Items[0].PayloadJson.ShouldBe("{\"symbol\":\"ETH\"}");
    }

    [Fact]
    public async Task GetRebalanceEvents_WhenFromFilterUsesZuluTimestamp_ShouldFilterEvents()
    {
        var tableServiceClient = CreateTableServiceClient(
            "rebalanceevents",
            [
                CreateRebalanceEventEntity("yolodaily", "run-1", 1, "RunStarted", "BTC"),
                CreateRebalanceEventEntity("yolodaily", "run-1", 2, "TradeProposed", "ETH"),
                CreateRebalanceEventEntity("yolodaily", "run-1", 3, "RunCompleted", "SOL")
            ]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/rebalance-events?strategy=yolodaily&from=2026-06-28T10:02:30Z",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetRebalanceEvents(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<RebalanceEventQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Select(item => item.EventType).ShouldBe(["RunCompleted"]);
    }

    [Fact]
    public async Task GetRebalanceEvents_WhenDateOnlyRangeProvided_ShouldFilterInclusiveUtcDates()
    {
        var tableServiceClient = CreateTableServiceClient(
            "rebalanceevents",
            [
                CreateRebalanceEventEntity(
                    "unraveldaily",
                    "run-1",
                    1,
                    "BeforeRange",
                    "BTC",
                    DateTimeOffset.Parse("2026-06-30T23:59:59+00:00")),
                CreateRebalanceEventEntity(
                    "unraveldaily",
                    "run-1",
                    2,
                    "StartOfRange",
                    "ETH",
                    DateTimeOffset.Parse("2026-07-01T00:00:00+00:00")),
                CreateRebalanceEventEntity(
                    "unraveldaily",
                    "run-1",
                    3,
                    "EndOfRange",
                    "SOL",
                    DateTimeOffset.Parse("2026-07-06T23:59:59+00:00")),
                CreateRebalanceEventEntity(
                    "unraveldaily",
                    "run-1",
                    4,
                    "AfterRange",
                    "ADA",
                    DateTimeOffset.Parse("2026-07-07T00:00:00+00:00"))
            ]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/rebalance-events?from=2026-07-01&to=2026-07-06",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetRebalanceEvents(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<RebalanceEventQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Select(item => item.EventType).ShouldBe(["StartOfRange", "EndOfRange"]);
    }

    [Fact]
    public async Task GetRebalanceEvents_WhenRawPagesContainNonMatches_ShouldFillFilteredPage()
    {
        var tableServiceClient = CreateTableServiceClient(
            "rebalanceevents",
            [
                CreateRebalanceEventEntity("yolodaily", "run-1", 1, "RunStarted", "BTC"),
                CreateRebalanceEventEntity("yolodaily", "run-1", 2, "TradeProposed", "ETH"),
                CreateRebalanceEventEntity("yolodaily", "run-1", 3, "RunCompleted", "BTC"),
                CreateRebalanceEventEntity("yolodaily", "run-1", 4, "TradeProposed", "SOL")
            ],
            continuationTokens: ["token-1", "token-2", "token-3", "token-4"]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/rebalance-events?eventType=TradeProposed&pageSize=2",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetRebalanceEvents(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<RebalanceEventQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Select(item => item.Coin).ShouldBe(["ETH", "SOL"]);
        payload.TotalCount.ShouldBe(2);
        payload.NextContinuationToken.ShouldBe("token-4");
    }

    [Fact]
    public async Task GetUserTrades_WhenTableHasRows_ShouldReturnFilteredTrades()
    {
        var tableServiceClient = CreateTableServiceClient(
            "usertrades",
            [
                CreateUserTradeEntity(
                    "yolodaily",
                    "Hyperliquid",
                    "testnet",
                    "0xwallet",
                    "0xvault",
                    "BTC",
                    "BTC",
                    "client-1",
                    123,
                    456,
                    DateTimeOffset.Parse("2026-07-09T10:15:00+00:00")),
                CreateUserTradeEntity(
                    "unraveldaily",
                    "Hyperliquid",
                    "testnet",
                    "0xwallet",
                    "0xvault",
                    "ETH",
                    "ETH",
                    "client-2",
                    124,
                    457,
                    DateTimeOffset.Parse("2026-07-09T11:15:00+00:00"))
            ]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/user-trades?strategy=yolodaily&exchange=Hyperliquid&network=testnet&vaultAddress=0xvault&symbol=BTC&clientOrderId=client-1&tradeId=456&from=2026-07-09T10:00:00Z&to=2026-07-09T10:30:00Z",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetUserTrades(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<UserTradeQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].StrategyName.ShouldBe("yolodaily");
        payload.Items[0].Exchange.ShouldBe("Hyperliquid");
        payload.Items[0].Network.ShouldBe("testnet");
        payload.Items[0].Symbol.ShouldBe("BTC");
        payload.Items[0].ClientOrderId.ShouldBe("client-1");
        payload.Items[0].OrderId.ShouldBe(123);
        payload.Items[0].TradeId.ShouldBe(456);
        payload.Items[0].RawJson.ShouldBe("{\"tid\":456}");
    }

    [Fact]
    public async Task GetUserTrades_WhenRawPagesContainNonMatches_ShouldFillFilteredPage()
    {
        var tableServiceClient = CreateTableServiceClient(
            "usertrades",
            [
                CreateUserTradeEntity("yolodaily", "Hyperliquid", "testnet", "0xwallet", null, "BTC", "BTC", "client-1", 1, 101),
                CreateUserTradeEntity("yolodaily", "Hyperliquid", "testnet", "0xwallet", null, "ETH", "ETH", "client-2", 2, 102),
                CreateUserTradeEntity("yolodaily", "Hyperliquid", "testnet", "0xwallet", null, "BTC", "BTC", "client-3", 3, 103),
                CreateUserTradeEntity("unraveldaily", "Hyperliquid", "testnet", "0xwallet", null, "BTC", "BTC", "client-4", 4, 104)
            ],
            continuationTokens: ["token-1", "token-2", "token-3", "token-4"]);
        var request = TestHttpRequestData.Create(
            "GET",
            "http://localhost/api/storage/user-trades?strategy=yolodaily&symbol=BTC&pageSize=2",
            services => services.AddSingleton(tableServiceClient));
        var sut = new StorageQueryFunctions(request.FunctionContext.InstanceServices, NullLogger<StorageQueryFunctions>.Instance);

        var response = await sut.GetUserTrades(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<PagedQueryResponse<UserTradeQueryItem>>(response);
        payload.ShouldNotBeNull();
        payload.Items.Select(item => item.ClientOrderId).ShouldBe(["client-1", "client-3"]);
        payload.TotalCount.ShouldBe(2);
        payload.NextContinuationToken.ShouldBe("token-3");
    }

    private static TableServiceClient CreateTableServiceClient(
        string tableName,
        IReadOnlyList<TableEntity> entities,
        string? continuationToken = null,
        IReadOnlyList<string?>? continuationTokens = null)
    {
        var pages = continuationTokens is null
            ? [Page<TableEntity>.FromValues(entities, continuationToken, Mock.Of<Response>())]
            : entities
                .Select((entity, index) => Page<TableEntity>.FromValues(
                    [entity],
                    index < continuationTokens.Count ? continuationTokens[index] : null,
                    Mock.Of<Response>()))
                .ToArray();
        var tableClient = new Mock<TableClient>();
        tableClient
            .Setup(x => x.QueryAsync<TableEntity>(
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<TableEntity>.FromPages(pages));

        var tableServiceClient = new Mock<TableServiceClient>();
        tableServiceClient
            .Setup(x => x.GetTableClient(tableName))
            .Returns(tableClient.Object);

        return tableServiceClient.Object;
    }

    private static TableEntity CreateHttpRequestEntity(string host, string endpoint, string contentHash) =>
        new(host, $"{endpoint}|{contentHash}")
        {
            ["Host"] = host,
            ["Endpoint"] = endpoint,
            ["Url"] = $"https://{host}{endpoint}",
            ["Method"] = "GET",
            ["StatusCode"] = 200,
            ["BlobContainer"] = "http-requests",
            ["BlobName"] = $"{host}{endpoint}/{contentHash}.json",
            ["ContentHash"] = contentHash,
            ["RequestTimeUtc"] = DateTimeOffset.Parse("2026-06-28T10:00:00+00:00"),
            ["QueryParametersJson"] = "{}"
        };

    private static TableEntity CreateUserTradeEntity(
        string strategyName,
        string exchange,
        string network,
        string address,
        string? vaultAddress,
        string symbol,
        string exchangeSymbol,
        string clientOrderId,
        long orderId,
        long tradeId,
        DateTimeOffset? timestampUtc = null)
    {
        var effectiveTimestamp = timestampUtc ?? DateTimeOffset.Parse("2026-07-09T10:00:00+00:00").AddMinutes(tradeId);
        var partitionAddress = vaultAddress ?? address;
        var entity = new TableEntity(
            $"{strategyName}|{exchange}|{network}|{partitionAddress}",
            $"{effectiveTimestamp:yyyyMMddHHmmssfffffff}|{tradeId}")
        {
            ["StrategyName"] = strategyName,
            ["Exchange"] = exchange,
            ["Network"] = network,
            ["Address"] = address,
            ["TimestampUtc"] = effectiveTimestamp,
            ["ExchangeSymbol"] = exchangeSymbol,
            ["Symbol"] = symbol,
            ["SymbolType"] = "Future",
            ["Direction"] = "OpenLong",
            ["Hash"] = $"hash-{tradeId}",
            ["OrderId"] = orderId,
            ["Price"] = "100000",
            ["OrderSide"] = "Buy",
            ["StartPosition"] = "0",
            ["Quantity"] = "0.01",
            ["Fee"] = "0.1",
            ["FeeToken"] = "USDC",
            ["ClosedPnl"] = "0",
            ["Crossed"] = true,
            ["TradeId"] = tradeId,
            ["ClientOrderId"] = clientOrderId,
            ["RawJson"] = $"{{\"tid\":{tradeId}}}"
        };

        if (!string.IsNullOrWhiteSpace(vaultAddress))
        {
            entity["VaultAddress"] = vaultAddress;
        }

        return entity;
    }

    private static TableEntity CreateRebalanceEventEntity(
        string strategyName,
        string runId,
        int sequence,
        string eventType,
        string coin) =>
        CreateRebalanceEventEntity(
            strategyName,
            runId,
            sequence,
            eventType,
            coin,
            DateTimeOffset.Parse("2026-06-28T10:00:00+00:00").AddMinutes(sequence));

    private static TableEntity CreateRebalanceEventEntity(
        string strategyName,
        string runId,
        int sequence,
        string eventType,
        string coin,
        DateTimeOffset timestampUtc) =>
        new($"{strategyName}|{runId}", $"202606281000000000000|{sequence:D6}|{eventType}")
        {
            ["RunId"] = runId,
            ["StrategyName"] = strategyName,
            ["TimestampUtc"] = timestampUtc,
            ["Sequence"] = sequence,
            ["EventType"] = eventType,
            ["Level"] = "Info",
            ["Summary"] = eventType,
            ["Coin"] = coin,
            ["PayloadJson"] = "{}"
        };

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
