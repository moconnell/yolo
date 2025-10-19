using System.Globalization;
using Moq;
using Shouldly;
using Unravel.Api.Interfaces;
using Xunit.Abstractions;
using YoloAbstractions;
using static YoloAbstractions.FactorType;

namespace Unravel.Api.Test;

public class UnravelFactorServiceTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public UnravelFactorServiceTest(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

    [Fact]
    public async Task GivenGoodConfig_WhenMocked_ShouldReturnFactors()
    {
        // arrange
        const string btc = "BTC";
        string[] tickers = [btc];
        const double retailFlowValueBtc = 0.25;

        var mockApiSvc = new Mock<IUnravelApiService>();
        mockApiSvc.Setup(apiSvc => apiSvc.GetFactorsLiveAsync(tickers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(tickers, DateTime.Today, (RetailFlow, [retailFlowValueBtc])));

        var svc = new UnravelFactorService(mockApiSvc.Object);

        // act
        var factors = await svc.GetFactorsAsync(tickers);

        // assert
        factors.ShouldNotBeNull();
        factors[btc].ShouldNotBeNull();
        factors[btc].Count.ShouldBe(1);
        var keyValuePair = factors[btc].First();
        keyValuePair.Key.ShouldBe(RetailFlow);
        keyValuePair.Value.ShouldBe(retailFlowValueBtc, 0.000000001);
    }
    
    [Fact]
    public async Task GivenDupeTickers_WhenMocked_ShouldReturnFactors()
    {
        // arrange
        const string btc = "BTC";
        string[] tickers = [btc];
        string[] dupeTickers = [btc, btc];
        const double retailFlowValueBtc = 0.25;

        var mockApiSvc = new Mock<IUnravelApiService>();
        mockApiSvc.Setup(apiSvc => apiSvc.GetFactorsLiveAsync(tickers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(tickers, DateTime.Today, (RetailFlow, [retailFlowValueBtc])));

        var svc = new UnravelFactorService(mockApiSvc.Object);

        // act
        var factors = await svc.GetFactorsAsync(dupeTickers);

        // assert
        factors.ShouldNotBeNull();
        factors[btc].ShouldNotBeNull();
        factors[btc].Count.ShouldBe(1);
        var keyValuePair = factors[btc].First();
        keyValuePair.Key.ShouldBe(RetailFlow);
        keyValuePair.Value.ShouldBe(retailFlowValueBtc, 0.000000001);
    }
}