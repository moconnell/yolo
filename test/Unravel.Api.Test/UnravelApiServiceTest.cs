using System.Net;
using Moq;
using Moq.Contrib.HttpClient;
using Shouldly;
using Unravel.Api.Config;
using YoloAbstractions;

namespace Unravel.Api.Test;

public class UnravelApiServiceTest
{
    [Fact]
    public async Task GivenGoodConfig_ShouldReturnFactors()
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
                $"{config.ApiBaseUrl}{string.Format(config.FactorsUrlPath, retailFlow, btc)}")
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
}