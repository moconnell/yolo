using Unravel.Api.Config;
using Unravel.Api.Interfaces;
using YoloAbstractions;
using static YoloAbstractions.FactorType;

namespace Unravel.Api.Test;

public class UnravelFactorServiceTest
{
    [Fact]
    public void GivenApiService_ShouldReturnFixedUniverse()
    {
        // arrange
        var mockApiSvc = new Mock<IUnravelApiService>();
        var svc = new UnravelFactorService(mockApiSvc.Object, new UnravelConfig());

        // act
        var isFixedUniverse = svc.IsFixedUniverse;

        // assert
        isFixedUniverse.ShouldBeFalse();
    }

    [Fact]
    public async Task GivenGoodConfig_WhenTickersSupplied_ShouldReturnFactors()
    {
        // arrange
        const string btc = "BTC";
        string[] tickers = [btc];
        const double retailFlowValueBtc = 0.25;

        var mockApiSvc = new Mock<IUnravelApiService>();
        mockApiSvc.Setup(apiSvc => apiSvc.GetFactorsLiveAsync(tickers, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(tickers, DateTime.Today, (RetailFlow, [retailFlowValueBtc])));

        var svc = new UnravelFactorService(mockApiSvc.Object, new UnravelConfig { UseLiveFactors = false });

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
    public async Task GivenGoodConfig_WhenNoTickersSupplied_ShouldFetchUniverse()
    {
        // arrange
        const string btc = "BTC";
        string[] tickers = [btc];
        const double retailFlowValueBtc = 0.25;

        var mockApiSvc = new Mock<IUnravelApiService>();

        mockApiSvc.Setup(apiSvc => apiSvc.GetUniverseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tickers);

        mockApiSvc
            .Setup(apiSvc => apiSvc.GetFactorsLiveAsync(tickers, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(tickers, DateTime.Today, (RetailFlow, [retailFlowValueBtc])));

        var svc = new UnravelFactorService(mockApiSvc.Object, new UnravelConfig { UseLiveFactors = false });

        // act
        var factors = await svc.GetFactorsAsync();

        // assert
        factors.ShouldNotBeNull();
        factors[btc].ShouldNotBeNull();
        factors[btc].Count.ShouldBe(1);
        var keyValuePair = factors[btc].First();
        keyValuePair.Key.ShouldBe(RetailFlow);
        keyValuePair.Value.ShouldBe(retailFlowValueBtc, 0.000000001);

        mockApiSvc.Verify(apiSvc => apiSvc.GetUniverseAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        mockApiSvc
            .Setup(apiSvc => apiSvc.GetFactorsLiveAsync(tickers, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(tickers, DateTime.Today, (RetailFlow, [retailFlowValueBtc])));

        var svc = new UnravelFactorService(mockApiSvc.Object, new UnravelConfig { UseLiveFactors = false });

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

    [Fact]
    public async Task GivenHistoricalConfig_WhenTickersSupplied_ShouldUseHistoricalEndpoint()
    {
        // arrange
        const string btc = "BTC";
        string[] tickers = [btc];
        const double retailFlowValueBtc = 0.25;

        var mockApiSvc = new Mock<IUnravelApiService>();
        mockApiSvc.Setup(apiSvc => apiSvc.GetFactorsHistoricalAsync(tickers, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                FactorDataFrame.NewFrom(tickers, DateTime.Today, (RetailFlow, [retailFlowValueBtc])));

        var svc = new UnravelFactorService(mockApiSvc.Object, new UnravelConfig { UseLiveFactors = true });

        // act
        var factors = await svc.GetFactorsAsync(tickers);

        // assert
        factors.ShouldNotBeNull();
        factors[btc].ShouldNotBeNull();
        factors[btc].Count.ShouldBe(1);
        var keyValuePair = factors[btc].First();
        keyValuePair.Key.ShouldBe(RetailFlow);
        keyValuePair.Value.ShouldBe(retailFlowValueBtc, 0.000000001);

        mockApiSvc.Verify(apiSvc => apiSvc.GetFactorsHistoricalAsync(tickers, false, It.IsAny<CancellationToken>()), Times.Once);
        mockApiSvc.Verify(apiSvc => apiSvc.GetFactorsLiveAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}