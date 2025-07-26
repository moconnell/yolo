using Xunit;

namespace YoloTrades.Test;

public class TickerExtensionsTests
{
    [Theory]
    [InlineData("BTC/USD", "BTC", "USD")]
    [InlineData("btc/usd", "BTC", "USD")]
    [InlineData("BTCUSD", "BTC", "USD")]
    [InlineData("1INCH/USD", "1INCH", "USD")]
    [InlineData("1INCH-USD", "1INCH", "USD")]
    [InlineData("1INCH/USDT", "1INCH", "USDT")]
    [InlineData("1INCH-USDC", "1INCH", "USDC")]
    [InlineData("1INCHUSD", "1INCH", "USD")]
    [InlineData("1INCHUSDT", "1INCH", "USDT")]
    [InlineData("BTC-USD", "BTC", "USD")]
    [InlineData("AVAX-USD", "AVAX", "USD")]
    [InlineData("AVAX-USDC", "AVAX", "USDC")]
    [InlineData("btcusdt", "BTC", "USDT")]
    [InlineData("BTCUSDC", "BTC", "USDC")]
    [InlineData("ETHusdt", "ETH", "USDT")]
    [InlineData("avaxusdt", "AVAX", "USDT")]
    public void GivenTickerSymbol_ShouldReturnExpectedBaseAndQuoteAssets(
        string ticker,
        string expectedBase,
        string expectedQuote)
    {
        // act
        var (baseAsset, quoteAsset) = ticker.GetBaseAndQuoteAssets();

        // assert
        Assert.Equal(expectedBase, baseAsset);
        Assert.Equal(expectedQuote, quoteAsset);
    }
}