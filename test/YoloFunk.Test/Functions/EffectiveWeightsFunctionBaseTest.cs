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

    private sealed class EffectiveWeightsFunctionHarness(IServiceProvider serviceProvider, ILogger<EffectiveWeightsFunctionHarness> logger)
        : EffectiveWeightsFunctionBase(serviceProvider, logger)
    {
        protected override string StrategyKey => "test";

        public Task<HttpResponseData> Run(HttpRequestData req, CancellationToken cancellationToken)
            => GetEffectiveWeightsAsync(req, cancellationToken);
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