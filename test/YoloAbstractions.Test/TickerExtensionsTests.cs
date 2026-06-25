using YoloAbstractions.Extensions;

namespace YoloAbstractions.Test;

public class TickerExtensionsTests
{
    [Theory]
    [InlineData("BTC/USD", "BTC", "USD")]
    [InlineData("btc/usd", "BTC", "USD")]
    [InlineData("bTc/usd", "BTC", "USD")]
    [InlineData("BTCUSD", "BTC", "USD")]
    [InlineData("1INCH/USD", "1INCH", "USD")]
    [InlineData("1INCH-USD", "1INCH", "USD")]
    [InlineData("1INCH/USDT", "1INCH", "USDT")]
    [InlineData("1INCH-USDC", "1INCH", "USDC")]
    [InlineData("1INCHUSD", "1INCH", "USD")]
    [InlineData("1INCHUSDT", "1INCH", "USDT")]
    [InlineData("RENDERUSD", "RENDER", "USD")]
    [InlineData("RENDERUSDC", "RENDER", "USDC")]
    [InlineData("SILVERUSD", "SILVER", "USD")]
    [InlineData("SILVERUSDT", "SILVER", "USDT")]
    [InlineData("BTC-USD", "BTC", "USD")]
    [InlineData("BTC-USDC", "BTC", "USDC")]
    [InlineData("BTC-USDE", "BTC", "USDE")]
    [InlineData("AVAX-USD", "AVAX", "USD")]
    [InlineData("AVAX-USDC", "AVAX", "USDC")]
    [InlineData("btcusdt", "BTC", "USDT")]
    [InlineData("BTCUSDC", "BTC", "USDC")]
    [InlineData("ETHusdt", "ETH", "USDT")]
    [InlineData("avaxusdt", "AVAX", "USDT")]
    [InlineData("1INCH", "1INCH", "")]
    [InlineData("AVAX", "AVAX", "")]
    [InlineData("BTC", "BTC", "")]
    [InlineData("RENDER", "RENDER", "")]
    [InlineData("SILVER", "SILVER", "")]
    [InlineData("SILVER-USDC", "SILVER", "USDC")]
    [InlineData("XYZ100", "XYZ100", "")]
    [InlineData("XYZ100-USDC", "XYZ100", "USDC")]
    [InlineData("S&P500", "S&P500", "")]
    [InlineData("S&P500-USDC", "S&P500", "USDC")]
    [InlineData("FARTCOIN", "FARTCOIN", "")]
    [InlineData("FARTCOIN-USDC", "FARTCOIN", "USDC")]
    [InlineData("IP", "IP", "")]
    [InlineData("IP-USDC", "IP", "USDC")]
    [InlineData("W", "W", "")]
    [InlineData("W-USDC", "W", "USDC")]
    [InlineData("PLATINUM", "PLATINUM", "")]
    [InlineData("PLATINUM-USDC", "PLATINUM", "USDC")]
    [InlineData("kPEPE", "kPEPE", "")]
    [InlineData("kPEPE-USDC", "kPEPE", "USDC")]
    [InlineData("2Z", "2Z", "")]
    [InlineData("2Z-USDC", "2Z", "USDC")]
    public void GivenTickerSymbol_ShouldReturnExpectedBaseAndQuoteAssets(
        string ticker,
        string expectedBase,
        string expectedQuote = "")
    {
        // act
        var (baseAsset, quoteAsset) = ticker.GetBaseAndQuoteAssets();

        // assert
        Assert.Equal(expectedBase, baseAsset);
        Assert.Equal(expectedQuote, quoteAsset);
    }
}
