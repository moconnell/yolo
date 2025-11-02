using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;
using YoloAbstractions.Extensions;

namespace YoloAbstractions.Test;

public class VolatilityExtensionsTest
{
    [Fact]
    public void AnnualizedVolatility_WithTwoPrices_CalculatesCorrectly()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 110m };

        // Act
        var volatility = closes.AnnualizedVolatility();

        // Assert
        volatility.ShouldBe(0);  // only one return period, so volatility is zero
    }

    [Fact]
    public void AnnualizedVolatility_WithConstantPrices_ReturnsZero()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 100m, 100m, 100m, 100m };

        // Act
        var volatility = closes.AnnualizedVolatility();

        // Assert
        Assert.Equal(0, volatility, 10);
    }

    [Fact]
    public void AnnualizedVolatility_WithIncreasingPrices_ReturnsPositiveVolatility()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility = closes.AnnualizedVolatility();

        // Assert
        Assert.True(volatility > 0);
    }

    [Fact]
    public void AnnualizedVolatility_WithDecreasingPrices_ReturnsPositiveVolatility()
    {
        // Arrange
        var closes = new List<decimal> { 120m, 115m, 110m, 105m, 100m };

        // Act
        var volatility = closes.AnnualizedVolatility();

        // Assert
        Assert.True(volatility > 0);
    }

    [Fact]
    public void AnnualizedVolatility_WithVolatilePrices_ReturnsHigherVolatility()
    {
        // Arrange
        var stablePrices = new List<decimal> { 100m, 101m, 102m, 103m, 104m };
        var volatilePrices = new List<decimal> { 100m, 120m, 90m, 130m, 80m };

        // Act
        var stableVolatility = stablePrices.AnnualizedVolatility();
        var volatileVolatility = volatilePrices.AnnualizedVolatility();

        // Assert
        Assert.True(volatileVolatility > stableVolatility);
    }

    [Fact]
    public void AnnualizedVolatility_WithCustomPeriodsPerYear_ScalesCorrectly()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility365 = closes.AnnualizedVolatility(periodsPerYear: 365);
        var volatility252 = closes.AnnualizedVolatility(periodsPerYear: 252);

        // Assert
        var expectedRatio = Math.Sqrt(365.0 / 252.0);
        var actualRatio = volatility365 / volatility252;
        Assert.Equal(expectedRatio, actualRatio, 5);
    }

    [Fact]
    public void AnnualizedVolatility_WithDefaultPeriodsPerYear_Uses365()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatilityDefault = closes.AnnualizedVolatility();
        var volatility365 = closes.AnnualizedVolatility(periodsPerYear: 365);

        // Assert
        Assert.Equal(volatility365, volatilityDefault);
    }

    [Fact]
    public void AnnualizedVolatility_WithSinglePrice_ThrowsArgumentException()
    {
        // Arrange
        var closes = new List<decimal> { 100m };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility());
        Assert.Contains("At least two closing prices are required", exception.Message);
    }

    [Fact]
    public void AnnualizedVolatility_WithEmptyList_ThrowsArgumentException()
    {
        // Arrange
        var closes = Array.Empty<decimal>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility());
        Assert.Contains("At least two closing prices are required", exception.Message);
    }

    [Fact]
    public void AnnualizedVolatility_WithZeroPrice_ThrowsArgumentException()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 0m, 110m };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility());
        Assert.Contains("All closing prices must be positive", exception.Message);
    }

    [Fact]
    public void AnnualizedVolatility_WithNegativePrice_ThrowsArgumentException()
    {
        // Arrange
        var closes = new List<decimal> { 100m, -50m, 110m };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => closes.AnnualizedVolatility());
        Assert.Contains("All closing prices must be positive", exception.Message);
    }

    [Fact]
    public void AnnualizedVolatility_WithLargePriceSwings_HandlesCorrectly()
    {
        // Arrange
        var closes = new List<decimal> { 1000m, 2000m, 500m, 3000m, 1500m };

        // Act
        var volatility = closes.AnnualizedVolatility();

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
        Assert.False(double.IsInfinity(volatility));
    }

    [Fact]
    public void AnnualizedVolatility_WithManyPrices_CalculatesCorrectly()
    {
        // Arrange
        var random = new Random(42);
        var closes = new List<decimal>();
        decimal price = 100m;
        for (int i = 0; i < 100; i++)
        {
            price *= (decimal) (1 + (random.NextDouble() - 0.5) * 0.02); // +/- 1% daily
            closes.Add(price);
        }

        // Act
        var volatility = closes.AnnualizedVolatility();

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
        Assert.False(double.IsInfinity(volatility));
    }

    [Fact]
    public void AnnualizedVolatility_WithEquityPeriodsPerYear_Uses252()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility = closes.AnnualizedVolatility(252);

        // Assert
        Assert.True(volatility > 0);
    }

    [Fact]
    public void AnnualizedVolatility_WithVerySmallChanges_HandlesCorrectly()
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
        var volatility = closes.AnnualizedVolatility();

        // Assert
        Assert.True(volatility >= 0);
        Assert.False(double.IsNaN(volatility));
    }

    [Fact]
    public void AnnualizedVolatility_WithAlternatingPrices_CalculatesCorrectly()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 110m, 100m, 110m, 100m, 110m };

        // Act
        var volatility = closes.AnnualizedVolatility();

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(252)]
    [InlineData(365)]
    public void AnnualizedVolatility_WithDifferentPeriodsPerYear_ProducesValidResults(int periodsPerYear)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m, 125m };

        // Act
        var volatility = closes.AnnualizedVolatility(periodsPerYear);

        // Assert
        Assert.True(volatility > 0);
        Assert.False(double.IsNaN(volatility));
        Assert.False(double.IsInfinity(volatility));
    }
}