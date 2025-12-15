using System.Net;
using System.Text;
using System.Text.Json;
using Moq.Protected;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Extensions;
using YoloAbstractions.Interfaces;

namespace YoloAbstractions.Test;

public class HttpClientExtensionsTest
{
    private class TestApiResponse : IApiResponse<string>
    {
        public IReadOnlyList<string> Data { get; set; } = [];
    }

    [Fact]
    public async Task GetAsync_WithValidResponse_ReturnsApiResponse()
    {
        var expectedData = new List<string> { "item1", "item2" };
        var apiResponse = new TestApiResponse { Data = expectedData };
        var httpClient = CreateHttpClient(HttpStatusCode.OK, apiResponse);

        var result = await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data");

        result.ShouldNotBeNull();
        result.Data.ShouldBe(expectedData);
    }

    [Fact]
    public async Task GetAsync_WithHeaders_AddsHeadersToRequest()
    {
        var apiResponse = new TestApiResponse { Data = ["item1"] };
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token123",
            ["X-Custom-Header"] = "custom-value"
        };

        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(HttpStatusCode.OK, apiResponse, req => capturedRequest = req);

        await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data", headers);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.GetValues("Authorization").ShouldContain("Bearer token123");
        capturedRequest.Headers.GetValues("X-Custom-Header").ShouldContain("custom-value");
    }

    [Fact]
    public async Task GetAsync_WithNullHttpClient_ThrowsArgumentNullException()
    {
        HttpClient httpClient = null!;

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data"));
    }

    [Fact]
    public async Task GetAsync_WithNullUrl_ThrowsArgumentException()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, new TestApiResponse { Data = ["item1"] });

        await Should.ThrowAsync<ArgumentException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>(null!));
    }

    [Fact]
    public async Task GetAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, new TestApiResponse { Data = ["item1"] });

        await Should.ThrowAsync<ArgumentException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>(""));
    }

    [Fact]
    public async Task GetAsync_WithUnsuccessfulStatusCode_ThrowsApiException()
    {
        var httpClient = CreateHttpClient<TestApiResponse>(HttpStatusCode.NotFound, null);

        var exception = await Should.ThrowAsync<ApiException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data"));

        exception.Message.ShouldContain("Could not fetch from API");
        exception.Message.ShouldContain("NotFound");
    }

    [Fact]
    public async Task GetAsync_WithNullResponse_ThrowsApiException()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, (TestApiResponse?)null);

        var exception = await Should.ThrowAsync<ApiException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data"));

        exception.Message.ShouldContain("No data returned from API");
    }

    [Fact]
    public async Task GetAsync_WithNullData_ThrowsApiException()
    {
        var apiResponse = new TestApiResponse { Data = null! };
        var httpClient = CreateHttpClient(HttpStatusCode.OK, apiResponse);

        var exception = await Should.ThrowAsync<ApiException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data"));

        exception.Message.ShouldContain("No data returned from API");
    }

    [Fact]
    public async Task GetAsync_WithEmptyData_ThrowsApiException()
    {
        var apiResponse = new TestApiResponse { Data = [] };
        var httpClient = CreateHttpClient(HttpStatusCode.OK, apiResponse);

        var exception = await Should.ThrowAsync<ApiException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data"));

        exception.Message.ShouldContain("No data returned from API");
    }

    [Fact]
    public async Task GetAsync_WithInvalidJson_ThrowsApiException()
    {
        var httpClient = CreateHttpClientWithContent(HttpStatusCode.OK, "invalid json content");

        var exception = await Should.ThrowAsync<ApiException>(async () =>
            await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data"));

        exception.Message.ShouldContain("Error deserializing API response");
        exception.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_PassesTokenToRequest()
    {
        var apiResponse = new TestApiResponse { Data = ["item1"] };
        var httpClient = CreateHttpClient(HttpStatusCode.OK, apiResponse);
        var cts = new CancellationTokenSource();

        await httpClient.GetAsync<TestApiResponse, string>("https://api.example.com/data", cancellationToken: cts.Token);

        cts.Token.IsCancellationRequested.ShouldBeFalse();
    }

    private static HttpClient CreateHttpClient<T>(HttpStatusCode statusCode, T? responseObject, Action<HttpRequestMessage>? requestCapture = null)
    {
        var json = JsonSerializer.Serialize(responseObject);
        return CreateHttpClientWithContent(statusCode, json, requestCapture);
    }

    private static HttpClient CreateHttpClientWithContent(HttpStatusCode statusCode, string content, Action<HttpRequestMessage>? requestCapture = null)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                requestCapture?.Invoke(req);
                return new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };
            });

        return new HttpClient(mockHandler.Object);
    }
}