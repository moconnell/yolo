using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Contrib.HttpClient;
using Shouldly;
using Unravel.Api.Config;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloAbstractions.Extensions;

namespace Unravel.Api.Test;

public class UnravelApiServiceTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public UnravelApiServiceTest(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

    [Fact]
    public async Task GivenGoodConfig_WhenMocked_ShouldReturnFactors()
    {
        // arrange
        const string btc = "BTC";
        const string retailFlow = "retail_flow";
        const int window = 10;

        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [new FactorConfig { Id = retailFlow, Type = FactorType.RetailFlow, Window = window }]
        };

        var data = Enumerable.Range(0, window).Select(n => n * 0.1 - 0.25).ToArray();
        var indices = Enumerable.Range(-window, window).Select(n => DateTime.Today.AddDays(n)).ToArray();
        var requestUrl =
            $"{config.ApiBaseUrl}/{string.Format(config.FactorsUrlPath, retailFlow, btc, indices[0].ToString(config.DateFormat))}";
        var indexCsv = indices.Select(d => d.ToString(config.DateFormat)).ToCsv("\", \"");
        var jsonContent = $$"""
                            {
                              "data": [
                                    [{{data.ToCsv("], [")}}]
                                ],
                              "index": ["{{indexCsv}}"],
                              "columns": [
                                    "BTC"
                                ]
                            }
                            """;

        handler.SetupRequest(HttpMethod.Get, requestUrl)
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(jsonContent),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var factors = await svc.GetFactorsAsync([btc]);

        // assert
        factors.ShouldNotBeNull();
        factors.ShouldNotBeEmpty();
        factors[btc].ShouldNotBeNull();
        factors[btc].Count.ShouldBe(1);
        var keyValuePair = factors[btc].First();
        keyValuePair.Key.ShouldBe(FactorType.RetailFlow);
        var expectedValue = Convert.ToDecimal(data.ZScore().Last());
        keyValuePair.Value.ShouldBe(
            new Factor("Ur.RetailFlow", FactorType.RetailFlow, btc, null, expectedValue, indices[^1]));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenGoodConfig_WhenLiveData_ShouldReturnFactors()
    {
        // arrange
        string[] tickers = ["BTC", "ADA", "ETH", "BNB", "TRX", "XRP", "SOL", "LINK", "AVAX", "DOGE"];

        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.local.json", true, true);
        var config = builder.Build();
        var unravelConfig = config.GetChildren().First().Get<UnravelConfig>();
        unravelConfig.ShouldNotBeNull();

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));

        using var httpClient = new HttpClient();

        var svc = new UnravelApiService(httpClient, unravelConfig);

        // act
        var factors = await svc.GetFactorsAsync(tickers, cancellationTokenSource.Token);

        // assert
        factors.ShouldNotBeNull();
        factors.ShouldNotBeEmpty();
        factors.Count.ShouldBe(tickers.Length);

        foreach (var ticker in tickers)
        {
            factors[ticker].ShouldNotBeNull();
            factors[ticker].Count.ShouldBe(1);
            var (factorType, factor) = factors[ticker].First();

            _testOutputHelper.WriteLine(factor.ToString());

            factorType.ShouldBe(FactorType.RetailFlow);
            factor.Id.ShouldBe("Ur.RetailFlow");
            factor.Ticker.ShouldBe(ticker);
            factor.Value.ShouldNotBe(0m);
            (DateTime.Today - factor.TimeStamp).ShouldBeLessThanOrEqualTo(TimeSpan.FromDays(1));
        }
    }
}