using System.Net;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Interfaces.Clients;
using HyperLiquid.Net.Interfaces.Clients.FuturesApi;
using HyperLiquid.Net.Objects.Models;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Config;
using YoloBroker.Hyperliquid.UserTrades;
using OrderSide = HyperLiquid.Net.Enums.OrderSide;

namespace YoloBroker.Hyperliquid.Test;

public sealed class HyperliquidUserTradeSourceTest
{
    [Fact]
    public async Task GetUserTradesByTimeAsync_WhenTradesReturned_ShouldMapRecordsAndUseVaultAddress()
    {
        string? requestedAddress = null;
        var timestamp = DateTime.SpecifyKind(DateTime.Parse("2026-07-09T10:15:00"), DateTimeKind.Utc);
        var source = CreateSource(
            [CreateTrade(timestamp)],
            vaultAddress: "0xvault",
            captureAddress: address => requestedAddress = address);

        var records = await source.GetUserTradesByTimeAsync(
            DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
            DateTimeOffset.Parse("2026-07-10T00:00:00+00:00"),
            CancellationToken.None);

        requestedAddress.ShouldBe("0xvault");
        var record = records.Single();
        record.Exchange.ShouldBe(Exchange.Hyperliquid);
        record.ExchangeSymbol.ShouldBe("BTC");
        record.Symbol.ShouldBe("BTC");
        record.SymbolType.ShouldBe(SymbolType.Futures.ToString());
        record.Crossed.ShouldBeTrue();
        record.Direction.ShouldBe(Direction.OpenLong.ToString());
        record.Hash.ShouldBe("0xhash");
        record.OrderId.ShouldBe(123);
        record.Price.ShouldBe("100000.5");
        record.OrderSide.ShouldBe(OrderSide.Buy.ToString());
        record.StartPosition.ShouldBe("0");
        record.Quantity.ShouldBe("0.01");
        record.TimestampUtc.ShouldBe(new DateTimeOffset(timestamp));
        record.Fee.ShouldBe("0.1");
        record.FeeToken.ShouldBe("USDC");
        record.BuilderFee.ShouldBe("0.01");
        record.TradeId.ShouldBe(456);
        record.TwapId.ShouldBe(789);
        record.ClientOrderId.ShouldBe("client-1");
        record.RawJson.ShouldContain("BTC");
    }

    [Fact]
    public async Task GetUserTradesByTimeAsync_WhenApiFails_ShouldThrow()
    {
        var source = CreateSource(
            [],
            httpResult: HttpResult<HyperLiquidUserTrade[]>(
                [],
                error: new TestError("500", new ErrorInfo(ErrorType.SystemError, "boom"), null)));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            source.GetUserTradesByTimeAsync(
                DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
                DateTimeOffset.Parse("2026-07-10T00:00:00+00:00"),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetUserTradesByTimeAsync_WhenNoAddressConfigured_ShouldThrowBeforeCallingApi()
    {
        var restClient = new Mock<IHyperLiquidRestClient>();
        var source = new HyperliquidUserTradeSource(
            restClient.Object,
            new HyperliquidConfig
            {
                Address = "",
                PrivateKey = "secret",
                UseTestnet = true
            });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            source.GetUserTradesByTimeAsync(
                DateTimeOffset.Parse("2026-07-09T00:00:00+00:00"),
                DateTimeOffset.Parse("2026-07-10T00:00:00+00:00"),
                CancellationToken.None));

        restClient.Verify(x => x.FuturesApi, Times.Never);
    }

    private static HyperliquidUserTradeSource CreateSource(
        HyperLiquidUserTrade[] trades,
        string? vaultAddress = null,
        Action<string?>? captureAddress = null,
        HttpResult<HyperLiquidUserTrade[]>? httpResult = null)
    {
        var restClient = new Mock<IHyperLiquidRestClient>();
        var futuresApi = new Mock<IHyperLiquidRestClientFuturesApi>();
        var trading = new Mock<IHyperLiquidRestClientFuturesApiTrading>();
        restClient.Setup(x => x.FuturesApi).Returns(futuresApi.Object);
        futuresApi.Setup(x => x.Trading).Returns(trading.Object);
        trading
            .Setup(x => x.GetUserTradesByTimeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<DateTime, DateTime?, bool?, string, CancellationToken>((_, _, _, address, _) =>
                captureAddress?.Invoke(address))
            .ReturnsAsync(httpResult ?? HttpResult(trades));

        return new HyperliquidUserTradeSource(
            restClient.Object,
            new HyperliquidConfig
            {
                Address = "0xwallet",
                PrivateKey = "secret",
                VaultAddress = vaultAddress,
                UseTestnet = true
            });
    }

    private static HyperLiquidUserTrade CreateTrade(DateTime timestamp) =>
        new()
        {
            ClosedPnl = 1.2m,
            ExchangeSymbol = "BTC",
            Symbol = "BTC",
            SymbolType = SymbolType.Futures,
            Crossed = true,
            Direction = Direction.OpenLong,
            Hash = "0xhash",
            OrderId = 123,
            Price = 100000.5m,
            OrderSide = OrderSide.Buy,
            StartPosition = 0m,
            Quantity = 0.01m,
            Timestamp = timestamp,
            Fee = 0.1m,
            FeeToken = "USDC",
            BuilderFee = 0.01m,
            TradeId = 456,
            TwapId = 789,
            ClientOrderId = "client-1"
        };

    private static HttpResult<T> HttpResult<T>(
        T result,
        HttpStatusCode httpStatusCode = HttpStatusCode.OK,
        Error? error = null) =>
        error is null
            ? CryptoExchange.Net.Objects.HttpResult.Ok(
                string.Empty,
                httpStatusCode,
                new Version(1, 1),
                null!,
                TimeSpan.Zero,
                null,
                string.Empty,
                0,
                string.Empty,
                string.Empty,
                HttpMethod.Get,
                null!,
                ResultDataSource.Server,
                result)
            : CryptoExchange.Net.Objects.HttpResult.Fail(
                string.Empty,
                httpStatusCode,
                new Version(1, 1),
                null!,
                TimeSpan.Zero,
                null,
                string.Empty,
                0,
                string.Empty,
                string.Empty,
                HttpMethod.Get,
                null!,
                ResultDataSource.Server,
                error,
                result);

    private sealed class TestError(string? errorCode, ErrorInfo errorInfo, Exception? exception)
        : Error(errorCode, errorInfo, exception);
}
