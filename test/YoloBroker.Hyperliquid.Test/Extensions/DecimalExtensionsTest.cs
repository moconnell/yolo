using YoloBroker.Hyperliquid.Extensions;

namespace YoloBroker.Hyperliquid.Test.Extensions;

public class DecimalExtensionsTest
{
    [Theory]
    [InlineData(123.456, 0.01, 123.46)]
    [InlineData(123.454, 0.01, 123.45)]
    [InlineData(100.123, 0.1, 100.1)]
    [InlineData(99.999, 0.01, 100.00)]
    [InlineData(0.12345, 0.001, 0.123)]
    public void RoundToValidPrice_WithValidInputs_ReturnsRoundedPrice(decimal price, decimal tickSize, decimal expected)
    {
        var result = price.RoundToValidPrice(tickSize);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RoundToValidPrice_WithZeroTickSize_ReturnsOriginalPrice()
    {
        var price = 123.456m;
        var result = price.RoundToValidPrice(0);
        Assert.Equal(price, result);
    }

    [Fact]
    public void RoundToValidPrice_WithNegativeTickSize_ReturnsOriginalPrice()
    {
        var price = 123.456m;
        var result = price.RoundToValidPrice(-0.01m);
        Assert.Equal(price, result);
    }

    [Theory]
    [InlineData(123.456, 0.01, 5, 0.01)]
    [InlineData(1.2345, 0.001, 5, 0.001)]
    [InlineData(12345.6, 0.01, 5, 1)]
    [InlineData(1234.56, 0.01, 5, 0.1)]
    [InlineData(12.3456, 0.001, 5, 0.001)]
    [InlineData(1.23456, 0.0001, 5, 0.0001)]
    public void CalculateValidTickSize_WithValidInputs_ReturnsCorrectTickSize(decimal price, decimal baseTickSize, int maxSigFigs, decimal expected)
    {
        var result = price.CalculateValidTickSize(baseTickSize, maxSigFigs);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateValidTickSize_WithZeroPrice_ReturnsBaseTickSize()
    {
        var baseTickSize = 0.01m;
        var result = 0m.CalculateValidTickSize(baseTickSize);
        Assert.Equal(baseTickSize, result);
    }

    [Fact]
    public void CalculateValidTickSize_WithNegativePrice_ReturnsBaseTickSize()
    {
        var baseTickSize = 0.01m;
        var result = (-123.45m).CalculateValidTickSize(baseTickSize);
        Assert.Equal(baseTickSize, result);
    }

    [Fact]
    public void CalculateValidTickSize_WithZeroBaseTickSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => 123.45m.CalculateValidTickSize(0));
    }

    [Fact]
    public void CalculateValidTickSize_WithNegativeBaseTickSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => 123.45m.CalculateValidTickSize(-0.01m));
    }

    [Fact]
    public void CalculateValidTickSize_WithNegativeMaxSignificantFigures_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => 123.45m.CalculateValidTickSize(0.01m, -1));
    }

    [Theory]
    [InlineData(0.001234, 0.0001, 5, 0.0001)]
    [InlineData(0.0001234, 0.00001, 5, 0.00001)]
    [InlineData(999999.99, 0.01, 5, 1)]
    public void CalculateValidTickSize_WithEdgeCases_ReturnsCorrectTickSize(decimal price, decimal baseTickSize, int maxSigFigs, decimal expected)
    {
        var result = price.CalculateValidTickSize(baseTickSize, maxSigFigs);
        Assert.Equal(expected, result);
    }
}