using CryptoExchange.Net.Objects;

namespace YoloBroker.Hyperliquid.Extensions;

public static class DecimalExtensions
{
    public static decimal RoundToValidPrice(this decimal price, decimal tickSize, RoundingType roundingType = RoundingType.Closest)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tickSize, 0, nameof(tickSize));

        // If tick size is zero or negative, return the original price
        if (tickSize <= 0) return price;

        // Apply 5 significant figures rule
        var validTickSize = CalculateValidTickSize(price, tickSize);

        // Round once with the correct tick size
        return roundingType switch
        {
            RoundingType.Down => Math.Floor(price / validTickSize) * validTickSize,
            RoundingType.Up => Math.Ceiling(price / validTickSize) * validTickSize,
            _ => Math.Round(price / validTickSize) * validTickSize,
        };
    }

    public static decimal CalculateValidTickSize(this decimal price, decimal baseTickSize, int maxSignificantFigures = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseTickSize, 0, nameof(baseTickSize));
        ArgumentOutOfRangeException.ThrowIfNegative(maxSignificantFigures, nameof(maxSignificantFigures));

        if (price <= 0) return baseTickSize;

        // Calculate how many digits are before the decimal point
        var integerPart = Math.Floor(price);
        var integerDigits = integerPart == 0 ? 0 : (int)Math.Floor(Math.Log10((double)integerPart)) + 1;

        // Calculate maximum allowed decimal places based on 5 sig figs rule
        var maxDecimalPlaces = Math.Max(0, maxSignificantFigures - integerDigits);

        // Calculate the tick size based on significant figures constraint
        var sigFigTickSize = (decimal)Math.Pow(10, -maxDecimalPlaces);

        // Use the larger of the two tick sizes (more restrictive)
        return Math.Max(baseTickSize, sigFigTickSize);
    }
}
