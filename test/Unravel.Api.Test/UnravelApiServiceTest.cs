using System.Net;
using Microsoft.Extensions.Configuration;
using Moq.Contrib.HttpClient;
using Unravel.Api.Config;
using Xunit.Abstractions;
using YoloAbstractions;
using YoloAbstractions.Exceptions;
using YoloAbstractions.Extensions;

namespace Unravel.Api.Test;

public class UnravelApiServiceTest(ITestOutputHelper outputHelper)
{
    private const string RetailFlow = "retail_flow";

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
            Factors = [FactorType.RetailFlow],
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

        outputHelper.WriteLine(result.ToString());

        // assert
        result.ShouldNotBeNull();
        result.FactorTypes.ShouldBe([]);
        result.Tickers.ShouldBe([]);
    }

    [Theory]
    [InlineData(FactorType.Unknown, true)]
    [InlineData((FactorType)999, true)]
    [InlineData(FactorType.RetailFlow, false)]
    public async Task GivenInvalidFactorType_WhenMocked_ShouldThrow(FactorType factorType, bool shouldThrow = false)
    {
        // arrange
        const string btc = "BTC";

        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [factorType],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        if (!shouldThrow)
        {
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
        }
        var svc = new UnravelApiService(httpClient, config);

        if (shouldThrow)
        {
            // act & assert
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await svc.GetFactorsLiveAsync([btc]));
            return;
        }

        // act
        var result = await svc.GetFactorsLiveAsync([btc]);

        outputHelper.WriteLine(result.ToString());

        // assert
        result.ShouldNotBeNull();
        result.FactorTypes.ShouldBe([factorType]);
        result.Tickers.ShouldBe([btc]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenGoodConfig_WhenLiveData_ShouldReturnFactors()
    {
        // arrange
        var tickers = "ADA,AVAX,BCH,BNB,BTC,DOGE,DOT,ETH,HBAR,HYPE,LINK,LTC,MNT,SHIB,SOL,SUI,TON,TRX,XLM,XRP".Split(',');


        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));

        using var httpClient = new HttpClient();
        var unravelConfig = GetConfig();
        var svc = new UnravelApiService(httpClient, unravelConfig);

        // act
        var result = await svc.GetFactorsLiveAsync(tickers, cancellationToken: cancellationTokenSource.Token);

        outputHelper.WriteLine(string.Empty);
        outputHelper.WriteLine(result.ToString());

        // assert
        result.ShouldNotBeNull();
        result.FactorTypes.ShouldBe(unravelConfig.Factors);
        result.Tickers.Count.ShouldBe(tickers.Length);
        result.Tickers.Except(tickers).ShouldBe([]);
        result[FactorType.RetailFlow, "MNT"].ShouldBe(double.NaN);
        foreach (var factor in unravelConfig.Factors)
        {
            if (factor == FactorType.TrendLongonlyAdaptive)
                continue; // can be zero
            tickers.All(ticker => result[factor, ticker] != 0).ShouldBe(true);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenGoodConfig_WhenLiveData_ShouldGetUniverse()
    {
        // arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));

        using var httpClient = new HttpClient();
        var unravelConfig = GetConfig();
        var svc = new UnravelApiService(httpClient, unravelConfig);

        // act
        var tickers = await svc.GetUniverseAsync(cancellationTokenSource.Token);

        outputHelper.WriteLine(tickers.ToCsv());

        // assert
        tickers.ShouldNotBeNull();
        tickers.Count.ShouldBe(unravelConfig.UniverseSize);
        tickers.ShouldAllBe(x => !string.IsNullOrEmpty(x));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenTickerUniverse_WhenNotAllReturned_ShouldPopulateNull()
    {
        // arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));

        using var httpClient = new HttpClient();
        var unravelConfig = GetConfig();
        var svc = new UnravelApiService(httpClient, unravelConfig);

        var tickers = "ADA,AVAX,BCH,BNB,BTC,DOGE,DOT,ETH,HBAR,HYPE,LINK,LTC,MNT,SHIB,SOL,SUI,TON,TRX,XLM,XRP".Split(',');

        // act
        var result = await svc.GetFactorsLiveAsync(tickers, cancellationToken: cancellationTokenSource.Token);

        outputHelper.WriteLine(result.ToString());

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(tickers);
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
            Factors = [FactorType.RetailFlow],
            UniverseSize = 10
        };

        var exchange = config.Exchange.ToString().ToLowerInvariant();
        var startDate = DateTime.Today.AddDays(-3).ToString(config.DateFormat);
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

        outputHelper.WriteLine(tickers.ToCsv());

        // assert
        tickers.ShouldNotBeNull();
        tickers.Count.ShouldBe(config.UniverseSize);
        tickers.ShouldAllBe(x => !string.IsNullOrEmpty(x));
    }

    [Fact]
    public async Task GivenMissingTickers_WhenThrowOnMissingValueFalse_ShouldPopulateNaN()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "ADA", "BTC", "ETH" };
        var tickersCsv = Uri.EscapeDataString(string.Join(",", requestedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [0.5, 0.7],
                            "index": "2023-06-30",
                            "columns": ["BTC", "ETH"] 
                        }
                        """), // Missing ADA
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var result = await svc.GetFactorsLiveAsync(requestedTickers, throwOnMissingValue: false);

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(requestedTickers);
        result[FactorType.RetailFlow, "ADA"].ShouldBe(double.NaN);
        result[FactorType.RetailFlow, "BTC"].ShouldBe(0.5, 0.000000001);
        result[FactorType.RetailFlow, "ETH"].ShouldBe(0.7, 0.000000001);
    }

    [Fact]
    public async Task GivenMissingValues_WhenThrowOnMissingValueFalse_ShouldPopulateNaN()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "ADA", "BTC", "ETH" };
        var tickersCsv = Uri.EscapeDataString(string.Join(",", requestedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [null, 0.5, 0.7],
                            "index": "2023-06-30",
                            "columns": ["ADA", "BTC", "ETH"] 
                        }
                        """), // Missing ADA
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var result = await svc.GetFactorsLiveAsync(requestedTickers, throwOnMissingValue: false);

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(requestedTickers);
        result[FactorType.RetailFlow, "ADA"].ShouldBe(double.NaN);
        result[FactorType.RetailFlow, "BTC"].ShouldBe(0.5, 0.000000001);
        result[FactorType.RetailFlow, "ETH"].ShouldBe(0.7, 0.000000001);
    }

    [Fact]
    public async Task GivenMissingTickers_WhenThrowOnMissingValueTrue_ShouldThrow()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "ADA", "BTC", "ETH" };
        var returnedTickers = new[] { "BTC", "ETH" }; // Missing ADA
        var tickersCsv = Uri.EscapeDataString(string.Join(",", requestedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [0.5, 0.7],
                            "index": "2023-06-30",
                            "columns": ["BTC", "ETH"]
                        }
                        """),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act & assert
        var ex = await Should.ThrowAsync<ApiException>(async () =>
            await svc.GetFactorsLiveAsync(requestedTickers, throwOnMissingValue: true));

        ex.Message.ShouldContain("Not all requested tickers were returned");
        ex.Message.ShouldContain("ADA");
    }

    [Fact]
    public async Task GivenMultipleMissingTickers_WhenThrowOnMissingValueFalse_ShouldPopulateAllNaN()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "ADA", "BTC", "ETH", "SOL", "XRP" };
        var returnedTickers = new[] { "BTC", "ETH" }; // Missing ADA, SOL, XRP
        var tickersCsv = Uri.EscapeDataString(string.Join(",", requestedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [0.5, 0.7],
                            "index": "2023-06-30",
                            "columns": ["BTC", "ETH"]
                        }
                        """),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var result = await svc.GetFactorsLiveAsync(requestedTickers, throwOnMissingValue: false);

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(requestedTickers);
        result[FactorType.RetailFlow, "ADA"].ShouldBe(double.NaN);
        result[FactorType.RetailFlow, "BTC"].ShouldBe(0.5, 0.000000001);
        result[FactorType.RetailFlow, "ETH"].ShouldBe(0.7, 0.000000001);
        result[FactorType.RetailFlow, "SOL"].ShouldBe(double.NaN);
        result[FactorType.RetailFlow, "XRP"].ShouldBe(double.NaN);
    }

    [Fact]
    public async Task GivenUnexpectedTickers_WhenReturned_ShouldThrow()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "BTC", "ETH" };
        var tickersCsv = Uri.EscapeDataString(string.Join(",", requestedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [0.3, 0.5, 0.7],
                            "index": "2023-06-30",
                            "columns": ["ADA", "BTC", "ETH"]
                        }
                        """),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act & assert
        var ex = await Should.ThrowAsync<ApiException>(async () =>
            await svc.GetFactorsLiveAsync(requestedTickers, throwOnMissingValue: false));

        ex.Message.ShouldContain("unexpected tickers");
        ex.Message.ShouldContain("ADA");
    }

    [Fact]
    public async Task GivenMissingTickerInMiddle_WhenReturned_ShouldInsertNaNAtCorrectPosition()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "ADA", "BTC", "ETH", "SOL" };
        var tickersCsv = Uri.EscapeDataString(string.Join(",", requestedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [0.3, 0.7, 0.9],
                            "index": "2023-06-30",
                            "columns": ["ADA", "ETH", "SOL"]
                        }
                        """),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var result = await svc.GetFactorsLiveAsync(requestedTickers, throwOnMissingValue: false);

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(requestedTickers);
        result[FactorType.RetailFlow, "ADA"].ShouldBe(0.3, 0.000000001);
        result[FactorType.RetailFlow, "BTC"].ShouldBe(double.NaN); // Missing, should be NaN
        result[FactorType.RetailFlow, "ETH"].ShouldBe(0.7, 0.000000001);
        result[FactorType.RetailFlow, "SOL"].ShouldBe(0.9, 0.000000001);
    }

    [Fact]
    public async Task GivenEmptyTickers_WhenCalled_ShouldThrowArgumentException()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var svc = new UnravelApiService(httpClient, config);

        // act & assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await svc.GetFactorsLiveAsync([]));
    }

    [Fact]
    public async Task GivenWhitespaceOnlyTickers_WhenCalled_ShouldThrowArgumentException()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var svc = new UnravelApiService(httpClient, config);

        // act & assert
        await Should.ThrowAsync<ArgumentException>(async () => await svc.GetFactorsLiveAsync(["  ", "\t", ""]));
    }

    [Fact]
    public async Task GivenTickersWithWhitespace_WhenCalled_ShouldTrimAndNormalize()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "  btc  ", "ETH\t", " ada " };
        var normalizedTickers = new[] { "ADA", "BTC", "ETH" }; // Sorted and uppercase
        var tickersCsv = Uri.EscapeDataString(string.Join(",", normalizedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [0.3, 0.5, 0.7],
                            "index": "2023-06-30",
                            "columns": ["ADA", "BTC", "ETH"]
                        }
                        """),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var result = await svc.GetFactorsLiveAsync(requestedTickers);

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(normalizedTickers);
    }

    [Fact]
    public async Task GivenDuplicateTickers_WhenCalled_ShouldDeduplicateAndSort()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new UnravelConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            Factors = [FactorType.RetailFlow],
            UrlPathFactorsLive = "/factors?id={0}&tickers={1}"
        };

        var requestedTickers = new[] { "BTC", "eth", "BTC", "ADA", "ETH" };
        var normalizedTickers = new[] { "ADA", "BTC", "ETH" }; // Deduplicated, sorted, uppercase
        var tickersCsv = Uri.EscapeDataString(string.Join(",", normalizedTickers));

        handler.SetupRequest(
                HttpMethod.Get,
                $"{config.ApiBaseUrl}/{string.Format(config.UrlPathFactorsLive, RetailFlow, tickersCsv)}")
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    Content = new StringContent(
                        """
                        {
                            "data": [0.3, 0.5, 0.7],
                            "index": "2023-06-30",
                            "columns": ["ADA", "BTC", "ETH"]
                        }
                        """),
                    StatusCode = HttpStatusCode.OK
                });

        var svc = new UnravelApiService(httpClient, config);

        // act
        var result = await svc.GetFactorsLiveAsync(requestedTickers);

        // assert
        result.ShouldNotBeNull();
        result.Tickers.ShouldBe(normalizedTickers);
    }

    private static UnravelConfig GetConfig()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.local.json", true)
            .AddEnvironmentVariables();

        var unravelConfig = builder
            .Build()
            .GetRequiredSection("Unravel")
            .Get<UnravelConfig>()
            .ShouldNotBeNull();

        return unravelConfig;
    }
}
