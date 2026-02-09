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
        int? quantiles = null)
    {
        if (method == None)
            return df;

        var result = df.Clone();

        foreach (var col in df.Columns)
        {
            if (col is not DoubleDataFrameColumn numeric)
                continue;

            result.Columns[col.Name] = numeric.Normalize(method, quantiles);
        }

        return result;
    }

    public static DoubleDataFrameColumn Normalize(this DoubleDataFrameColumn col, NormalizationMethod method, int? quantiles = null)
    {
        if (method == None)
            return col;

        if (method == CrossSectionalBins && (!quantiles.HasValue || quantiles <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(quantiles), quantiles, $"Quantiles must be a positive integer when using {CrossSectionalBins} normalization.");
        }

        var normalizedValues = method switch
        {
            CrossSectionalBins => col.NormalizeBins(quantiles!.Value),
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

    // -------------------------------
    // Internal helpers (column-level)
    // -------------------------------
    internal static double[] NormalizeBins(
        this DoubleDataFrameColumn col,
        int quantiles)
    {
        var items = new List<(double Value, int Index)>();

        for (int i = 0; i < col.Length; i++)
        {
            var v = col[i];
            if (v.HasValue && !double.IsNaN(v.Value))
                items.Add((v.Value, i));
        }

        if (items.Count == 0)
            return [.. Enumerable.Repeat(double.NaN, (int)col.Length)];

        // qcut behaviour
        items.Sort((a, b) => a.Value.CompareTo(b.Value));

        int n = items.Count;
        var bins = new int[n];

        for (int k = 0; k < n; k++)
        {
            var b = (int)Math.Floor((k + 1) / (double)n * quantiles) - 1;
            if (b < 0) b = 0;
            if (b > quantiles - 1) b = quantiles - 1;
            bins[k] = b;
        }

        int maxBin = bins.Max();
        if (maxBin <= 0)
        {
            return EmptyArray(col, items, n);
        }

        var weights = new double[n];
        for (int k = 0; k < n; k++)
            weights[k] = 2.0 * (bins[k] / (double)maxBin) - 1.0;

        // L1 normalisation
        var denom = weights.Sum(w => Math.Abs(w));
        if (denom <= 0)
        {
            return EmptyArray(col, items, n);
        }

        for (int k = 0; k < n; k++)
            weights[k] /= denom;

        double[] result = [.. Enumerable.Repeat(double.NaN, (int)col.Length)];
        for (int k = 0; k < n; k++)
            result[items[k].Index] = weights[k];

        return result;

        static double[] EmptyArray(DoubleDataFrameColumn col, List<(double Value, int Index)> items, int n)
        {
            double[] result = [.. Enumerable.Repeat(double.NaN, (int)col.Length)];
            for (int k = 0; k < n; k++)
                result[items[k].Index] = 0.0;
            return result;
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