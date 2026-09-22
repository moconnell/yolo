using YoloAbstractions.Extensions;
using static YoloAbstractions.Extensions.VolatilityMethod;

namespace YoloAbstractions.Test;

public class VolatilityExtensionsTest
{
    [Theory]
    [InlineData(Simple, 0.1)]
    [InlineData(Logarithmic, 0.10033534773107558)]
    public void AnnualizedVolatility_WithReturnType_CalculatesExpectedValue(VolatilityMethod volatilityMethod, double expected)
    {
        // Arrange: simple returns are +10% and -10%; log returns are ln(1.1) and ln(0.9).
        var closes = new List<decimal> { 100m, 110m, 99m };

        // Act
        var volatility = closes.AnnualizedVolatility(periodsPerYear: 1, volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBe(expected, tolerance: 1e-12);
    }

    [Fact]
    public void AnnualizedVolatility_WithUnsupportedMethod_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var closes = new List<decimal> { 100m, 110m };
        var unsupportedMethod = (VolatilityMethod)int.MaxValue;

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            closes.AnnualizedVolatility(volatilityMethod: unsupportedMethod));

        // Assert
        exception.ParamName.ShouldBe("volatilityMethod");
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithTwoPrices_CalculatesCorrectly(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 110m };

        // Act
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBe(0);  // only one return period, so volatility is zero
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithConstantPrices_ReturnsZero(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 100m, 100m, 100m, 100m };

        // Act
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBe(0, tolerance: 1e-10);
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithIncreasingPrices_ReturnsPositiveVolatility(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithDecreasingPrices_ReturnsPositiveVolatility(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 120m, 115m, 110m, 105m, 100m };

        // Act
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithVolatilePrices_ReturnsHigherVolatility(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var stablePrices = new List<decimal> { 100m, 101m, 102m, 103m, 104m };
        var volatilePrices = new List<decimal> { 100m, 120m, 90m, 130m, 80m };

        // Act
        var stableVolatility = stablePrices.AnnualizedVolatility(volatilityMethod: volatilityMethod);
        var volatileVolatility = volatilePrices.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatileVolatility.ShouldBeGreaterThan(stableVolatility);
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithCustomPeriodsPerYear_ScalesCorrectly(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility365 = closes.AnnualizedVolatility(periodsPerYear: 365, volatilityMethod: volatilityMethod);
        var volatility252 = closes.AnnualizedVolatility(periodsPerYear: 252, volatilityMethod: volatilityMethod);

        // Assert
        var expectedRatio = Math.Sqrt(365.0 / 252.0);
        var actualRatio = volatility365 / volatility252;
        actualRatio.ShouldBe(expectedRatio, tolerance: 1e-5);
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithDefaultPeriodsPerYear_Uses365(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatilityDefault = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);
        var volatility365 = closes.AnnualizedVolatility(periodsPerYear: 365, volatilityMethod: volatilityMethod);

        // Assert
        volatilityDefault.ShouldBe(volatility365);
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithSinglePrice_ThrowsArgumentException(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => closes.AnnualizedVolatility(volatilityMethod: volatilityMethod));
        exception.Message.ShouldContain("At least two closing prices are required");
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithEmptyList_ThrowsArgumentException(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = Array.Empty<decimal>();

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => closes.AnnualizedVolatility(volatilityMethod: volatilityMethod));
        exception.Message.ShouldContain("At least two closing prices are required");
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithZeroPrice_ThrowsArgumentException(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 0m, 110m };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => closes.AnnualizedVolatility(volatilityMethod: volatilityMethod));
        exception.Message.ShouldContain("All closing prices must be positive");
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithNegativePrice_ThrowsArgumentException(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, -50m, 110m };

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => closes.AnnualizedVolatility(volatilityMethod: volatilityMethod));
        exception.Message.ShouldContain("All closing prices must be positive");
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithLargePriceSwings_HandlesCorrectly(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 1000m, 2000m, 500m, 3000m, 1500m };

        // Act
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThan(0);
        double.IsNaN(volatility).ShouldBeFalse();
        double.IsInfinity(volatility).ShouldBeFalse();
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithManyPrices_CalculatesCorrectly(VolatilityMethod volatilityMethod)
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
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThan(0);
        double.IsNaN(volatility).ShouldBeFalse();
        double.IsInfinity(volatility).ShouldBeFalse();
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithEquityPeriodsPerYear_Uses252(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m };

        // Act
        var volatility = closes.AnnualizedVolatility(252, volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithVerySmallChanges_HandlesCorrectly(VolatilityMethod volatilityMethod)
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
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThanOrEqualTo(0);
        double.IsNaN(volatility).ShouldBeFalse();
    }

    [Theory]
    [InlineData(Simple)]
    [InlineData(Logarithmic)]
    public void AnnualizedVolatility_WithAlternatingPrices_CalculatesCorrectly(VolatilityMethod volatilityMethod)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 110m, 100m, 110m, 100m, 110m };

        // Act
        var volatility = closes.AnnualizedVolatility(volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThan(0);
        double.IsNaN(volatility).ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, Simple)]
    [InlineData(30, Simple)]
    [InlineData(252, Simple)]
    [InlineData(365, Simple)]
    [InlineData(-1, Simple, true)]
    [InlineData(0, Simple, true)]
    [InlineData(400, Simple, true)]
    [InlineData(1, Logarithmic)]
    [InlineData(30, Logarithmic)]
    [InlineData(252, Logarithmic)]
    [InlineData(365, Logarithmic)]
    [InlineData(-1, Logarithmic, true)]
    [InlineData(0, Logarithmic, true)]
    [InlineData(400, Logarithmic, true)]
    public void AnnualizedVolatility_WithDifferentPeriodsPerYear_ProducesValidResults(int periodsPerYear, VolatilityMethod volatilityMethod, bool shouldThrow = false)
    {
        // Arrange
        var closes = new List<decimal> { 100m, 105m, 110m, 115m, 120m, 125m };

        if (shouldThrow)
        {
            // Act & Assert
            Should.Throw<ArgumentOutOfRangeException>(() => closes.AnnualizedVolatility(periodsPerYear, volatilityMethod: volatilityMethod));
            return;
        }

        // Act
        var volatility = closes.AnnualizedVolatility(periodsPerYear, volatilityMethod: volatilityMethod);

        // Assert
        volatility.ShouldBeGreaterThan(0);
        double.IsNaN(volatility).ShouldBeFalse();
        double.IsInfinity(volatility).ShouldBeFalse();
    }
}
