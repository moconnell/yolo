using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace YoloAbstractions.Extensions;

public static class StatsExtensions
{
    public static IEnumerable<double> ZScore(this IReadOnlyList<double> series)
    {
        var mean = series.Mean();
        var std = series.StandardDeviation();
        return series.Select(x => (x - mean) / std);
    }
}