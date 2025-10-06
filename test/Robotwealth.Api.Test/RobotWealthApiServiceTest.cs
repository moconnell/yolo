using System.Net;
using Moq;
using Moq.Contrib.HttpClient;
using RobotWealth.Api;
using Shouldly;
using RobotWealth.Api.Config;
using YoloAbstractions;

namespace Robotwealth.Api.Test;

public class RobotWealthApiServiceTest
{
    [Fact]
    public async Task GivenGoodConfig_ShouldReturnFactors()
    {
        // arrange
        const string btcUsdt = "BTCUSDT";

        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new RobotWealthConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            ApiKey = Guid.NewGuid().ToString(),
            VolatilitiesUrlPath = "volatilities",
            WeightsUrlPath = "weights"
        };

        SetupRequest(config.VolatilitiesUrlPath, "Data/volatilities.json");
        SetupRequest(config.WeightsUrlPath, "Data/weights.json");

        var svc = new RobotWealthApiService(httpClient, config);

        // act
        var factors = await svc.GetFactorsAsync([btcUsdt]);

        // assert
        factors.ShouldNotBeNull();
        factors.ShouldNotBeEmpty();
        var btcUsdtFactors = factors[btcUsdt];
        btcUsdtFactors.ShouldNotBeNull();
        btcUsdtFactors.Count.ShouldBe(4);
        btcUsdtFactors[FactorType.Carry]
            .ShouldBe(
                new Factor(
                    "Rw.Carry",
                    FactorType.Carry,
                    btcUsdt,
                    123846.55m,
                    -0.116056661080926m,
                    new DateTime(2025, 10, 6)));
        btcUsdtFactors[FactorType.Momentum]
            .ShouldBe(
                new Factor(
                    "Rw.Momentum",
                    FactorType.Momentum,
                    btcUsdt,
                    123846.55m,
                    0.129485645933014m,
                    new DateTime(2025, 10, 6)));
        btcUsdtFactors[FactorType.Trend]
            .ShouldBe(
                new Factor(
                    "Rw.Trend",
                    FactorType.Trend,
                    btcUsdt,
                    123846.55m,
                    0.0853393962230077m,
                    new DateTime(2025, 10, 6)));
        btcUsdtFactors[FactorType.Volatility]
            .ShouldBe(
                new Factor(
                    "Rw.Volatility",
                    FactorType.Volatility,
                    btcUsdt,
                    null,
                    0.272582342594688m,
                    new DateTime(2025, 10, 6)));
        return;

        void SetupRequest(string methodPath, string responseFilePath)
        {
            var requestUrl = $"{config.ApiBaseUrl}/{methodPath}?api_key={Uri.EscapeDataString(config.ApiKey)}";
            var json = GetFileContent(responseFilePath);
            var httpResponseMessage = new HttpResponseMessage
            {
                Content = new StringContent(json),
                StatusCode = HttpStatusCode.OK
            };

            handler.SetupRequest(HttpMethod.Get, requestUrl)
                .ReturnsAsync(httpResponseMessage);
        }
    }

    private static string GetFileContent(string fileName) =>
        File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), fileName));
}