using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YoloAbstractions;
using YoloAbstractions.Config;
using YoloAbstractions.Interfaces;
using YoloBroker.Interface;
using YoloFunk.Dto;
using YoloFunk.Functions;

namespace YoloFunk.Test.Functions;

public class EffectiveWeightsFunctionBaseTest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GivenMissingBrokerAddressContext_WhenRun_ShouldReturnInternalServerError()
    {
        var services = TestHttpRequestData.CreateInstanceServices(new BrokerAccountContext(null, null));

        var request = TestHttpRequestData.Create("GET", "http://localhost/api/rebalance/test/effective-weights", services);
        var sut = new EffectiveWeightsFunctionHarness(request.FunctionContext.InstanceServices, NullLogger<EffectiveWeightsFunctionHarness>.Instance);

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("test");
        payload.Error.ShouldBe("Invalid strategy configuration");
    }

    [Fact]
    public async Task GivenEmptyBrokerAddressContext_WhenRun_ShouldReturnInternalServerError()
    {
        var services = TestHttpRequestData.CreateInstanceServices(new BrokerAccountContext(string.Empty, "0x1111111111111111111111111111111111111111"));

        var request = TestHttpRequestData.Create("GET", "http://localhost/api/rebalance/test/effective-weights", services);
        var sut = new EffectiveWeightsFunctionHarness(request.FunctionContext.InstanceServices, NullLogger<EffectiveWeightsFunctionHarness>.Instance);

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("test");
        payload.Error.ShouldBe("Invalid strategy configuration");
    }

    [Fact]
    public async Task GivenValidStrategyServices_WhenRun_ShouldReturnEffectiveWeights()
    {
        var (response, accountContext) = await RunEffectiveWeightsAsync(
            positionAmount: 2m,
            rawTargetWeight: 0.4m,
            tradeBuffer: 0.01m,
            rebalanceMode: RebalanceMode.Center);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<EffectiveWeightsResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("test");
        payload.Address.ShouldBe(accountContext.Address);
        payload.Nominal.ShouldBe(1000m);
        payload.CurrentGrossExposure.ShouldBe(0.198m);
        payload.CurrentNetExposure.ShouldBe(0.198m);
        payload.EffectiveGrossExposure.ShouldBe(0.4m);
        payload.EffectiveNetExposure.ShouldBe(0.4m);
        payload.BufferAdjustedGrossExposure.ShouldBe(0.4m);
        payload.BufferAdjustedNetExposure.ShouldBe(0.4m);
        payload.Weights.ShouldNotBeEmpty();
        var sol = payload.Weights.Single(x => x.Token == "SOL");
        sol.RawFactors.ShouldNotBeNull();
        sol.RawFactors["Carry"].ShouldBe(0.2d);
        sol.RawFactors["Volatility"].ShouldBe(0.4d);
        sol.NormalizedFactors.ShouldNotBeNull();
        sol.NormalizedFactors["Carry"].ShouldBe(1d);
        sol.NormalizedFactors["Volatility"].ShouldBe(0.4d);
    }

    [Fact]
    public async Task GivenCurrentWeightWithinTradeBuffer_WhenRun_ShouldUseCurrentExposureAsEffectiveExposure()
    {
        var (response, _) = await RunEffectiveWeightsAsync(
            positionAmount: 4m,
            rawTargetWeight: 0.4m,
            tradeBuffer: 0.01m,
            rebalanceMode: RebalanceMode.Center);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<EffectiveWeightsResponse>(response);
        payload.ShouldNotBeNull();
        payload.CurrentGrossExposure.ShouldBe(0.396m);
        payload.CurrentNetExposure.ShouldBe(0.396m);
        payload.EffectiveGrossExposure.ShouldBe(0.4m);
        payload.EffectiveNetExposure.ShouldBe(0.4m);
        payload.BufferAdjustedGrossExposure.ShouldBe(0.396m);
        payload.BufferAdjustedNetExposure.ShouldBe(0.396m);

        var sol = payload.Weights.Single(x => x.Token == "SOL");
        sol.CurrentWeight.ShouldBe(0.396m);
        sol.ConstrainedTargetWeight.ShouldBe(0.4m);
        sol.EffectiveWeight.ShouldBe(0.4m);
        sol.BufferAdjustedTargetWeight.ShouldBe(0.396m);
        sol.DeltaWeight.ShouldBe(0m);
        sol.WithinTradeBuffer.ShouldBeTrue();
    }

    [Fact]
    public async Task GivenCurrentWeightWithinTradeBuffer_WhenRun_ShouldKeepEffectiveExposureConsistentWithEffectiveWeights()
    {
        var (response, _) = await RunEffectiveWeightsAsync(
            positionAmount: 4m,
            rawTargetWeight: 0.4m,
            tradeBuffer: 0.01m,
            rebalanceMode: RebalanceMode.Center);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<EffectiveWeightsResponse>(response);
        payload.ShouldNotBeNull();

        payload.EffectiveGrossExposure.ShouldBe(payload.Weights.Sum(item => Math.Abs(item.EffectiveWeight.GetValueOrDefault())));
        payload.EffectiveNetExposure.ShouldBe(payload.Weights.Sum(item => item.EffectiveWeight.GetValueOrDefault()));
        payload.BufferAdjustedGrossExposure.ShouldBe(0.396m);
        payload.Weights.Single(x => x.Token == "SOL").DeltaWeight.ShouldBe(0m);
    }

    [Fact]
    public async Task GivenEdgeRebalanceMode_WhenRun_ShouldPreserveTargetExposure()
    {
        var (response, _) = await RunEffectiveWeightsAsync(
            positionAmount: 2m,
            rawTargetWeight: 0.4m,
            tradeBuffer: 0.01m,
            rebalanceMode: RebalanceMode.Edge);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await TestHttpRequestData.ReadJsonAsync<EffectiveWeightsResponse>(response);
        payload.ShouldNotBeNull();
        payload.CurrentGrossExposure.ShouldBe(0.198m);
        payload.CurrentNetExposure.ShouldBe(0.198m);
        payload.EffectiveGrossExposure.ShouldBe(0.4m);
        payload.EffectiveNetExposure.ShouldBe(0.4m);
        payload.BufferAdjustedGrossExposure.ShouldBe(0.4m);
        payload.BufferAdjustedNetExposure.ShouldBe(0.4m);

        var sol = payload.Weights.Single(x => x.Token == "SOL");
        sol.CurrentWeight.ShouldBe(0.198m);
        sol.ConstrainedTargetWeight.ShouldBe(0.4m);
        sol.EffectiveWeight.ShouldBe(0.4m);
        sol.BufferAdjustedTargetWeight.ShouldBe(0.4m);
        sol.DeltaWeight.ShouldBe(0.202m);
        sol.WithinTradeBuffer.ShouldBeFalse();
    }

    [Fact]
    public async Task GivenDuplicateNormalizedBaseAssets_WhenRun_ShouldReturnBadRequestWithDetails()
    {
        const string strategy = "test";
        var accountContext = new BrokerAccountContext("0x1111111111111111111111111111111111111111", null);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.GetAccountContext()).Returns(accountContext);
        broker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Position>>());
        broker.Setup(x => x.GetMarketsAsync(
                It.IsAny<ISet<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<MarketInfo>>());

        var weights = new Mock<ICalcWeights>();
        weights.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(WeightsCalculationResult.FromWeights(
                new Dictionary<string, decimal>
                {
                    ["BTC"] = 0.4m,
                    ["BTC/USDC"] = 0.2m
                }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<WorkerOptions>()
            .Configure(options => options.Serializer = new JsonObjectSerializer());
        services.AddKeyedSingleton(strategy, broker.Object);
        services.AddKeyedSingleton(strategy, weights.Object);
        services.AddKeyedSingleton(strategy, new YoloConfig
        {
            BaseAsset = "USDC",
            NominalCash = 1000m,
            MaxLeverage = 2m,
            TradeBuffer = 0.01m,
            RebalanceMode = RebalanceMode.Center,
            AssetPermissions = AssetPermissions.All
        });

        using var provider = services.BuildServiceProvider();
        var request = TestHttpRequestData.Create("GET", "http://localhost/api/rebalance/test/effective-weights", provider);
        var sut = new EffectiveWeightsFunctionHarness(request.FunctionContext.InstanceServices, NullLogger<EffectiveWeightsFunctionHarness>.Instance);

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe(strategy);
        payload.Error.ShouldBe("Invalid raw weights");
        payload.Details.ShouldNotBeNull();
        payload.Details.ShouldContain("BTC");
        payload.Details.ShouldContain("BTC/USDC");
    }

    private sealed class EffectiveWeightsFunctionHarness(IServiceProvider serviceProvider, ILogger<EffectiveWeightsFunctionHarness> logger)
        : EffectiveWeightsFunctionBase(serviceProvider, logger)
    {
        protected override string StrategyKey => "test";

        public Task<HttpResponseData> Run(HttpRequestData req, CancellationToken cancellationToken)
            => GetEffectiveWeightsAsync(req, cancellationToken);
    }

    private static async Task<(HttpResponseData Response, BrokerAccountContext AccountContext)> RunEffectiveWeightsAsync(
        decimal positionAmount,
        decimal rawTargetWeight,
        decimal tradeBuffer,
        RebalanceMode rebalanceMode)
    {
        const string strategy = "test";
        var accountContext = new BrokerAccountContext("0x1111111111111111111111111111111111111111", null);

        var broker = new Mock<IYoloBroker>();
        broker.Setup(x => x.GetAccountContext()).Returns(accountContext);
        broker.Setup(x => x.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<Position>>
            {
                ["SOL"] =
                [
                    new Position("SOL-PERP", "SOL", AssetType.Future, positionAmount)
                ]
            });
        broker.Setup(x => x.GetMarketsAsync(
                It.IsAny<ISet<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<AssetPermissions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, IReadOnlyList<MarketInfo>>
            {
                ["SOL"] =
                [
                    new MarketInfo(
                        Name: "SOL-PERP",
                        BaseAsset: "SOL",
                        QuoteAsset: "USDC",
                        AssetType: AssetType.Future,
                        TimeStamp: DateTime.UtcNow,
                        Ask: 100m,
                        Bid: 99m,
                        Last: 99.5m,
                        PriceStep: 0.1m,
                        QuantityStep: 0.01m,
                        MinProvideSize: 0.01m)
                ]
            });

        var weights = new Mock<ICalcWeights>();
        weights.Setup(x => x.CalculateWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeightsCalculationResult(
                new Dictionary<string, decimal>
                {
                    ["SOL"] = rawTargetWeight
                },
                FactorDataFrame.NewFrom(
                    ["SOL/USDC"],
                    DateTime.Today,
                    (FactorType.Carry, [0.2d]),
                    (FactorType.Volatility, [0.4d])),
                FactorDataFrame.NewFrom(
                    ["SOL/USDC"],
                    DateTime.Today,
                    (FactorType.Carry, [1d]),
                    (FactorType.Volatility, [0.4d]))));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<WorkerOptions>()
            .Configure(options => options.Serializer = new JsonObjectSerializer());
        services.AddKeyedSingleton(strategy, broker.Object);
        services.AddKeyedSingleton(strategy, weights.Object);
        services.AddKeyedSingleton(strategy, new YoloConfig
        {
            BaseAsset = "USDC",
            NominalCash = 1000m,
            MaxLeverage = 2m,
            TradeBuffer = tradeBuffer,
            RebalanceMode = rebalanceMode,
            AssetPermissions = AssetPermissions.All
        });

        using var provider = services.BuildServiceProvider();
        var request = TestHttpRequestData.Create("GET", "http://localhost/api/rebalance/test/effective-weights", provider);
        var sut = new EffectiveWeightsFunctionHarness(request.FunctionContext.InstanceServices, NullLogger<EffectiveWeightsFunctionHarness>.Instance);

        return (await sut.Run(request, CancellationToken.None), accountContext);
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

        public static TestHttpRequestData Create(string method, string url, IServiceProvider? instanceServices = null)
        {
            var functionContext = new Mock<FunctionContext>();
            functionContext
                .SetupGet(x => x.InstanceServices)
                .Returns(instanceServices ?? CreateInstanceServices());

            return new TestHttpRequestData(functionContext.Object, method, url);
        }

        public static IServiceProvider CreateInstanceServices()
            => CreateInstanceServices(null);

        public static IServiceProvider CreateInstanceServices(BrokerAccountContext? accountContext)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<WorkerOptions>()
                .Configure(options => options.Serializer = new JsonObjectSerializer());

            if (accountContext is not null)
            {
                var broker = new Mock<IYoloBroker>();
                broker.Setup(x => x.GetAccountContext())
                    .Returns(accountContext);
                services.AddKeyedSingleton("test", broker.Object);
            }

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
