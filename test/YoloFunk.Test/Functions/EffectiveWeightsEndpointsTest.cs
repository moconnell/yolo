using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using YoloFunk.Dto;
using YoloFunk.Functions;

namespace YoloFunk.Test.Functions;

public class EffectiveWeightsEndpointsTest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task YoloDailyEffectiveWeights_WhenServicesMissing_ShouldReturnInternalServerError()
    {
        var request = TestHttpRequestData.Create("GET", "http://localhost/api/rebalance/yolodaily/effective-weights");
        var sut = new YoloDailyEffectiveWeights(request.FunctionContext.InstanceServices, NullLogger<YoloDailyEffectiveWeights>.Instance);

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("yolodaily");
    }

    [Fact]
    public async Task UnravelDailyEffectiveWeights_WhenServicesMissing_ShouldReturnInternalServerError()
    {
        var request = TestHttpRequestData.Create("GET", "http://localhost/api/rebalance/unraveldaily/effective-weights");
        var sut = new UnravelDailyEffectiveWeights(request.FunctionContext.InstanceServices, NullLogger<UnravelDailyEffectiveWeights>.Instance);

        var response = await sut.Run(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var payload = await TestHttpRequestData.ReadJsonAsync<RebalanceErrorResponse>(response);
        payload.ShouldNotBeNull();
        payload.Strategy.ShouldBe("unraveldaily");
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

        public static TestHttpRequestData Create(string method, string url)
        {
            var services = new ServiceCollection();
            services.AddOptions<WorkerOptions>()
                .Configure(options => options.Serializer = new JsonObjectSerializer());

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
