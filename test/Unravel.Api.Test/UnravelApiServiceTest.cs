using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Contrib.HttpClient;
using Shouldly;
using Unravel.Api.Config;
using YoloAbstractions;

namespace Unravel.Api.Test;

public class UnravelApiServiceTest
{
    private const string RetailFlow = "retail-flow";

    [Fact]
    public async Task GivenGoodConfig_WhenMocked_ShouldReturnFactors()
    {
        // arrange
        const string btc = "BTC";

        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [new FactorConfig { Id = RetailFlow, Type = FactorType.RetailFlow }],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };
        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, btc)}")
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
        var result = await svc.GetFactorsLiveAsync([btc]);

        // assert
        result.ShouldNotBeNull();
        result.FactorTypes.ShouldBe([FactorType.RetailFlow]);
        result.Tickers.ShouldBe([btc]);
        result[FactorType.RetailFlow, btc].ShouldBe(0.25, 0.000000001);
    }

    [Fact]
    public async Task GivenEmptyConfig_WhenMocked_ShouldReturnEmpty()
    {
        // arrange
        const string btc = "BTC";

        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig();
        var svc = new UnravelApiService(httpClient, config);

        // act
        var result = await svc.GetFactorsLiveAsync([btc]);

        // assert
        result.ShouldNotBeNull();
        result.FactorTypes.ShouldBe([]);
        result.Tickers.ShouldBe([]);
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
        var result = await svc.GetFactorsLiveAsync(tickers, cancellationTokenSource.Token);

        // assert
        result.ShouldNotBeNull();
        result.FactorTypes.ShouldBe([FactorType.RetailFlow]);
        result.Tickers.Except(tickers).ShouldBe([]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenGoodConfig_WhenLiveData_ShouldGetUniverse()
    {
        // arrange
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
        var tickers = await svc.GetUniverseAsync(cancellationTokenSource.Token);

        // assert
        tickers.ShouldNotBeNull();
        tickers.Count.ShouldBe(unravelConfig.UniverseSize);
        tickers.ShouldAllBe(x => !string.IsNullOrEmpty(x));
    }

    [Fact]
    public async Task GivenGoodConfig_WhenMocked_ShouldGetUniverse()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [new FactorConfig { Id = RetailFlow, Type = FactorType.RetailFlow }],
            UniverseSize = 10
        };

        var exchange = config.Exchange.ToString().ToLowerInvariant();
        var startDate = DateTime.Today.AddDays(-2).ToString(config.DateFormat);
        var requestUrl =
            $"{config.ApiBaseUrl}/{string.Format(config.UrlPathUniverse, config.UniverseSize, exchange, startDate)}";
        handler.SetupRequest(HttpMethod.Get, requestUrl)
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        $$"""
                          {
                              "data": [
                                  [1,1,1,1,1,1,1,1,1,1]
                              ],
                              "index": ["{{startDate}}"],
                              "columns": [
                                  "BTC", "ADA", "ETH", "BNB", "TRX", "XRP", "SOL", "LINK", "AVAX", "DOGE"
                              ]
                          }
                          """),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var tickers = await svc.GetUniverseAsync();

        // assert
        tickers.ShouldNotBeNull();
        tickers.Count.ShouldBe(config.UniverseSize);
        tickers.ShouldAllBe(x => !string.IsNullOrEmpty(x));
    }
}