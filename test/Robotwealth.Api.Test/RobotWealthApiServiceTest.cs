using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Contrib.HttpClient;
using Shouldly;
using RobotWealth.Api.Config;

namespace RobotWealth.Api.Test;

public class RobotWealthApiServiceTest
{
    private const string BtcUsdt = "BTCUSDT";

    [Fact]
    public async Task GivenGoodConfig_ShouldGetWeights()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new RobotWealthConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            ApiKey = Guid.NewGuid().ToString(),
            VolatilitiesUrlPath = "volatilities",
            WeightsUrlPath = "weights"
        };

        SetupRequest(config.WeightsUrlPath, "Data/weights.json", config, handler);

        var svc = new RobotWealthApiService(httpClient, config);

        // act
        var weights = await svc.GetWeightsAsync();

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(10);
        var weight = weights.FirstOrDefault(x => x.Ticker == BtcUsdt);
        weight.ShouldNotBeNull();
        weight.CarryMegafactor.ShouldBe(-0.116056661080926);
        weight.MomentumMegafactor.ShouldBe(0.129485645933014);
        weight.TrendMegafactor.ShouldBe(0.0853393962230077);
    }

    [Fact]
    public async Task GivenGoodConfig_ShouldGetVolatilities()
    {
        // arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = handler.CreateClient();
        var config = new RobotWealthConfig
        {
            ApiBaseUrl = "http://foo.org/api",
            ApiKey = Guid.NewGuid().ToString(),
            VolatilitiesUrlPath = "volatilities",
            WeightsUrlPath = "weights"
        };

        SetupRequest(config.VolatilitiesUrlPath, "Data/volatilities.json", config, handler);

        var svc = new RobotWealthApiService(httpClient, config);

        // act
        var volatilities = await svc.GetVolatilitiesAsync();

        // assert
        volatilities.ShouldNotBeNull();
        volatilities.Count.ShouldBe(10);
        var volBtc = volatilities.FirstOrDefault(x => x.Ticker == BtcUsdt);
        volBtc.ShouldNotBeNull();
        volBtc.EwVol.ShouldBe(0.272582342594688);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenGoodConfig_WhenLiveData_ShouldGetWeights()
    {
        // arrange
        var rwConfig = GetRobotWealthConfigFromAppSettings();

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));

        using var httpClient = new HttpClient();

        var svc = new RobotWealthApiService(httpClient, rwConfig);

        // act
        var weights = await svc.GetWeightsAsync(cancellationTokenSource.Token);

        // assert
        weights.ShouldNotBeNull();
        weights.Count.ShouldBe(10);
        weights.ShouldAllBe(x => x.Date > DateTime.MinValue);
        weights.ShouldAllBe(x => !string.IsNullOrEmpty(x.Ticker));
        weights.ShouldAllBe(x => x.ArrivalPrice != 0);
        weights.ShouldAllBe(x => x.CarryMegafactor != 0);
        weights.ShouldAllBe(x => x.MomentumMegafactor != 0);
        weights.ShouldAllBe(x => x.TrendMegafactor != 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenGoodConfig_WhenLiveData_ShouldGetVolatilities()
    {
        // arrange
        var rwConfig = GetRobotWealthConfigFromAppSettings();

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));

        using var httpClient = new HttpClient();

        var svc = new RobotWealthApiService(httpClient, rwConfig);

        // act
        var volatilities = await svc.GetVolatilitiesAsync(cancellationTokenSource.Token);

        // assert
        volatilities.ShouldNotBeNull();
        volatilities.Count.ShouldBe(10);
        volatilities.ShouldAllBe(x => x.Date > DateTime.MinValue);
        volatilities.ShouldAllBe(x => !string.IsNullOrEmpty(x.Ticker));
        volatilities.ShouldAllBe(x => x.EwVol != 0);
    }

    private static RobotWealthConfig GetRobotWealthConfigFromAppSettings()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.local.json", true);
        var config = builder.Build();
        var rwConfig = config.GetRequiredSection("RobotWealth").Get<RobotWealthConfig>();
        rwConfig.ShouldNotBeNull();
        return rwConfig;
    }

    private static void SetupRequest(
        string methodPath,
        string responseFilePath,
        RobotWealthConfig config,
        Mock<HttpMessageHandler> handler)
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

    private static string GetFileContent(string fileName) =>
        File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), fileName));
}