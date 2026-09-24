using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Data.Analysis;

using static YoloAbstractions.NormalizationMethod;

namespace YoloAbstractions.Extensions;

public static class DataFrameExtensions
{
    public static DataFrame Normalize(
        this DataFrame df,
        NormalizationMethod method = None,
        int? quantiles = null,
        int precision = 12)
    {
        if (method == None)
            return df;

        var result = df.Clone();

        foreach (var col in df.Columns)
        {
            if (col is not DoubleDataFrameColumn numeric)
                continue;

            result.Columns[col.Name] = numeric.Normalize(method, quantiles, precision);
        }

        return result;
    }

    public static DoubleDataFrameColumn Normalize(
        this DoubleDataFrameColumn col,
        NormalizationMethod method,
        int? quantiles = null,
        int precision = 12)
    {
        if (method == None)
            return col;

        if (method == CrossSectionalBins && (!quantiles.HasValue || quantiles <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(quantiles), quantiles, $"Quantiles must be a positive integer when using {CrossSectionalBins} normalization.");
        }

        var normalizedValues = method switch
        {
            CrossSectionalBins => col.NormalizeBins(quantiles!.Value, precision),
            CrossSectionalZScore => col.NormalizeZScore(),
            MinMax => col.NormalizeMinMax(),
            Rank => col.NormalizeRank(),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, $"Unknown normalization method: {method}")
        };

        return new DoubleDataFrameColumn(col.Name, normalizedValues);
    }

    public static DoubleDataFrameColumn PointwiseDivide(this DoubleDataFrameColumn col, MathNet.Numerics.LinearAlgebra.Vector<double> divisor)
    {
        if (col.Length != divisor.Count)
            throw new ArgumentException($"Column length ({col.Length}) must match divisor length ({divisor.Count}).", nameof(divisor));

        var vec = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.DenseOfArray([.. col.Select(x => x ?? double.NaN)]);
        var resultVec = vec.PointwiseDivide(divisor);

        return new DoubleDataFrameColumn(col.Name, resultVec);
    }

    public static DoubleDataFrameColumn NormalizeGrossAbs(this DoubleDataFrameColumn col, double targetGross = 1.0)
    {
        var values = col.Select(v => v ?? double.NaN).ToArray();
        var gross = values.Sum(x => Math.Abs(x));
        if (gross <= 0 || double.IsNaN(gross) || double.IsInfinity(gross))
            return col;

        var scale = targetGross / gross;
        var normalizedValues = values.Select(v => v * scale).ToArray();

        return new DoubleDataFrameColumn(col.Name, normalizedValues);
    }

    // -------------------------------
    // Internal helpers (column-level)
    // -------------------------------
    internal static double[] NormalizeBins(
        this DoubleDataFrameColumn col,
        int quantiles,
        int precision = 12)
    {
        if (precision is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(precision), precision, "Precision must be between 0 and 15.");

        var items = new List<(double Value, int Index)>();

        for (int i = 0; i < col.Length; i++)
        {
            var v = col[i];
            if (v.HasValue && !double.IsNaN(v.Value))
                items.Add((Math.Round(v.Value, precision), i));
        }

        if (items.Count == 0)
            return [.. Enumerable.Repeat(double.NaN, (int)col.Length)];

        var sortedValues = items.Select(x => x.Value).Order().ToArray();
        var edges = Enumerable.Range(0, quantiles + 1)
            .Select(i => Quantile(sortedValues, i / (double)quantiles))
            .Distinct()
            .ToArray();

        // pandas.qcut(..., duplicates: "drop") has no usable interval when every
        // quantile edge is identical. Preserve missing values and mark all valid
        // observations as NaN so an uninformative factor is excluded downstream.
        if (edges.Length < 2)
            return [.. Enumerable.Repeat(double.NaN, (int)col.Length)];

        var innerEdges = edges[1..^1];
        var maxBin = edges.Length - 2;
        if (maxBin <= 0)
            return [.. Enumerable.Repeat(double.NaN, (int)col.Length)];

        var weights = new double[items.Count];
        for (var k = 0; k < items.Count; k++)
        {
            // qcut intervals are right-closed: a value equal to a boundary stays
            // in the lower bin. Equal values therefore always receive equal bins.
            var bin = LowerBound(innerEdges, items[k].Value);
            weights[k] = 2.0 * (bin / (double)maxBin) - 1.0;
        }

        // L1 normalisation
        var denom = weights.Sum(w => Math.Abs(w));
        if (denom <= 0)
            return [.. Enumerable.Repeat(double.NaN, (int)col.Length)];

        for (int k = 0; k < items.Count; k++)
            weights[k] /= denom;

        double[] result = [.. Enumerable.Repeat(double.NaN, (int)col.Length)];
        for (int k = 0; k < items.Count; k++)
            result[items[k].Index] = weights[k];

        return result;

        static double Quantile(IReadOnlyList<double> sorted, double probability)
        {
            var position = (sorted.Count - 1) * probability;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            if (lower == upper)
                return sorted[lower];

            var fraction = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }

        static int LowerBound(IReadOnlyList<double> values, double target)
        {
            var low = 0;
            var high = values.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (values[middle] < target)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }
    }

    internal static IEnumerable<double> NormalizeZScore(this DoubleDataFrameColumn col)
    {
        var values = col.Where(v => v.HasValue && !double.IsNaN(v.Value)).Select(v => v!.Value).ToArray();

        if (values.Length == 0)
            return col.Select(v => double.NaN);

        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Length;
        var stdDev = Math.Sqrt(variance);

        if (stdDev < 1e-10) // Avoid division by zero for constant columns
            return col.Select(v => 0.0);

        return col.Select(v => v.HasValue && !double.IsNaN(v.Value)
            ? (v.Value - mean) / stdDev
            : double.NaN);
    }

    internal static IEnumerable<double> NormalizeMinMax(this DoubleDataFrameColumn col)
    {
        var values = col.Where(v => v.HasValue && !double.IsNaN(v.Value)).Select(v => v!.Value).ToArray();

        if (values.Length == 0)
            return col.Select(v => double.NaN);

        var min = values.Min();
        var max = values.Max();
        var range = max - min;

        if (range < 1e-10) // Avoid division by zero
            return col.Select(v => 0.0);

        return col.Select(v => v.HasValue && !double.IsNaN(v.Value)
            ? 2 * ((v.Value - min) / range) - 1 // Scale to [-1, 1]
            : double.NaN);
    }

    internal static IEnumerable<double> NormalizeRank(this DoubleDataFrameColumn col)
    {
        var values = col.Select((v, i) => (Value: v, Index: i)).ToArray();
        var validValues = values
            .Where(x => x.Value.HasValue && !double.IsNaN(x.Value.Value))
            .OrderBy(x => x.Value!.Value)
            .ToArray();

        if (validValues.Length == 0)
            return col.Select(v => double.NaN);

        var ranks = new Dictionary<int, double>();
        for (var i = 0; i < validValues.Length; i++)
        {
            // Scale to [-1, 1] range
            ranks[validValues[i].Index] = validValues.Length > 1
                ? 2.0 * i / (validValues.Length - 1) - 1
                : 0.0;
        }

        return values.Select(x => ranks.TryGetValue(x.Index, out var rank) ? rank : double.NaN);
    }
}
