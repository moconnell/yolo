using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Contrib.HttpClient;
using Shouldly;
using Unravel.Api.Config;
using Xunit.Abstractions;
using YoloAbstractions;

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
        const string retailFlow = "retail-flow";

        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [new FactorConfig { Id = retailFlow, Type = FactorType.RetailFlow }],
            FactorsUrlPath = "/factors?id={0}&tickers={1}"
        };
        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.FactorsUrlPath, retailFlow, btc)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [
                                0.25
                            ],
                            "index": "2023-06-30",
                            "columns": [
                                "BTC"
                            ]
                        }
                        """),
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
        keyValuePair.Value.ShouldBe(
            new Factor("Ur.RetailFlow", FactorType.RetailFlow, btc, null, 0.25m, new DateTime(2023, 6, 30)));
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
