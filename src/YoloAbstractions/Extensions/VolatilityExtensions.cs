using System;
using System.Collections.Generic;
using System.Linq;

namespace YoloAbstractions.Extensions;

public static class VolatilityExtensions
{
    /// <summary>
    /// Calculates the annualized volatility of a series of closing prices.
    /// </summary>
    /// <param name="closes">Sequence of closing prices (oldest to newest).</param>
    /// <param name="periodsPerYear">
    /// Number of periods per year (252 for equities, 365 for crypto daily data).
    /// </param>
    public static double AnnualizedVolatility(this IEnumerable<decimal> closes, int periodsPerYear = 365)
    {
        var prices = closes.ToList();
        if (prices.Count < 2)
            throw new ArgumentException("At least two closing prices are required.");
        if (prices.Any(x => x <= 0))
            throw new ArgumentException("All closing prices must be positive.");

        // Compute log returns
        var logReturns = new List<double>(prices.Count - 1);
        for (var i = 1; i < prices.Count; i++)
        {

            var r = Math.Log((double) (prices[i] / prices[i - 1]));
            logReturns.Add(r);
        }

        // Standard deviation of log returns
        var mean = logReturns.Average();
        var variance = logReturns.Average(r => Math.Pow(r - mean, 2));
        var stdev = Math.Sqrt(variance);

        // Annualize
        return stdev * Math.Sqrt(periodsPerYear);
    }
}