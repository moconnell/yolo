using YoloAbstractions.Extensions;

namespace YoloAbstractions.Test;

public class VolatilityExtensionsTest
{
    [Theory]
    [InlineData(false, 0.1)]
    [InlineData(true, 0.10033534773107558)]
    public void AnnualizedVolatility_WithReturnType_CalculatesExpectedValue(bool useLogReturns, double expected)
    {
        // Arrange: simple returns are +10% and -10%; log returns are ln(1.1) and ln(0.9).
        var closes = new List<decimal> { 100m, 110m, 99m };

        // Act
        var volatility = closes.AnnualizedVolatility(periodsPerYear: 1, useLogReturns: useLogReturns);

        // Assert
        Assert.Equal(expected, volatility, 12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithTwoPrices_CalculatesCorrectly(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 110m };

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        volatility.ShouldBe(0);  // only one return period, so volatility is zero
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithConstantPrices_ReturnsZero(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 100m, 100m, 100m, 100m };

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.Equal(0, volatility, 10);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithIncreasingPrices_ReturnsPositiveVolatility(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithDecreasingPrices_ReturnsPositiveVolatility(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 120m, 115m, 110m, 105m, 100m };

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithVolatilePrices_ReturnsHigherVolatility(bool useLogReturns)
    {
        // Arrange
        var stablePrices = new List<decimal> { 100m, 101m, 102m, 103m, 104m };
        var volatilePrices = new List<decimal> { 100m, 120m, 90m, 130m, 80m };

        // Act
        var stableVolatility = stablePrices.AnnualizedVolatility(useLogReturns: useLogReturns);
        var volatileVolatility = volatilePrices.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatileVolatility > stableVolatility);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithCustomPeriodsPerYear_ScalesCorrectly(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility365 = closes.AnnualizedVolatility(periodsPerYear: 365, useLogReturns: useLogReturns);
        var volatility252 = closes.AnnualizedVolatility(periodsPerYear: 252, useLogReturns: useLogReturns);

        // Assert
        var expectedRatio = Math.Sqrt(365.0 / 252.0);
        var actualRatio = volatility365 / volatility252;
        Assert.Equal(expectedRatio, actualRatio, 5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithDefaultPeriodsPerYear_Uses365(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatilityDefault = closes.AnnualizedVolatility(useLogReturns: useLogReturns);
        var volatility365 = closes.AnnualizedVolatility(periodsPerYear: 365, useLogReturns: useLogReturns);

        // Assert
        Assert.Equal(volatility365, volatilityDefault);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithSinglePrice_ThrowsArgumentException(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility(useLogReturns: useLogReturns));
        Assert.Contains("At least two closing prices are required", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithEmptyList_ThrowsArgumentException(bool useLogReturns)
    {
        // Arrange
        var closes = Array.Empty<decimal>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility(useLogReturns: useLogReturns));
        Assert.Contains("At least two closing prices are required", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithZeroPrice_ThrowsArgumentException(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 0m, 110m };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility(useLogReturns: useLogReturns));
        Assert.Contains("All closing prices must be positive", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithNegativePrice_ThrowsArgumentException(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, -50m, 110m };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility(useLogReturns: useLogReturns));
        Assert.Contains("All closing prices must be positive", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithLargePriceSwings_HandlesCorrectly(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 1000m, 2000m, 500m, 3000m, 1500m };

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
        Assert.False(double.IsInfinity(volatility));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithManyPrices_CalculatesCorrectly(bool useLogReturns)
    {
        // Arrange
        var random = new Random(42);
        var closes = new List<decimal>();
        decimal price = 100m;
        for (int i = 0; i < 100; i++)
        {
            price *= (decimal)(1 + (random.NextDouble() - 0.5) * 0.02); // +/- 1% daily
            closes.Add(price);
        }

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
        Assert.False(double.IsInfinity(volatility));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithEquityPeriodsPerYear_Uses252(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility = closes.AnnualizedVolatility(252, useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithVerySmallChanges_HandlesCorrectly(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal>
        {
            100.0000m,
            100.0001m,
            100.0002m,
            100.0001m,
            100.0003m
        };

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility >= 0);
        Assert.False(double.IsNaN(volatility));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnualizedVolatility_WithAlternatingPrices_CalculatesCorrectly(bool useLogReturns)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 110m, 100m, 110m, 100m, 110m };

        // Act
        var volatility = closes.AnnualizedVolatility(useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(30, false)]
    [InlineData(252, false)]
    [InlineData(365, false)]
    [InlineData(-1, false, true)]
    [InlineData(0, false, true)]
    [InlineData(400, false, true)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(252, true)]
    [InlineData(365, true)]
    [InlineData(-1, true, true)]
    [InlineData(0, true, true)]
    [InlineData(400, true, true)]
    public void AnnualizedVolatility_WithDifferentPeriodsPerYear_ProducesValidResults(int periodsPerYear, bool useLogReturns, bool shouldThrow = false)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m, 125m };

        if (shouldThrow)
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => closes.AnnualizedVolatility(periodsPerYear, useLogReturns: useLogReturns));
            return;
        }

        // Act
        var volatility = closes.AnnualizedVolatility(periodsPerYear, useLogReturns: useLogReturns);

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
        Assert.False(double.IsInfinity(volatility));
    }
}
