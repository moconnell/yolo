using Microsoft.Data.Analysis;

namespace YoloAbstractions.Extensions;

public static class DataFrameExtensions
{
    public static DataFrame Normalize(
        this DataFrame df,
        NormalizationMethod method = NormalizationMethod.None,
        int? quantiles = null)
    {
        if (method == NormalizationMethod.CrossSectionalBins && (!quantiles.HasValue || quantiles <= 0))
        {
            throw new ArgumentException($"{nameof(quantiles)}: quantiles must be a positive integer when using CrossSectionalBins normalization.");
        }

        return method switch
        {
            NormalizationMethod.None => df,
            NormalizationMethod.CrossSectionalBins => df.NormalizeBins(quantiles!.Value),
            _ => throw new ArgumentException($"Unsupported normalization method: {method}")
        };
    }

    /// <summary>
    /// Cross-sectional q-tile binning across rows (tickers) for each numeric column.
    /// This matches Unravel create_cross_sectional_bins()[0].
    /// </summary>
    public static DataFrame NormalizeBins(
        this DataFrame df,
        int quantiles = 10)
    {
        var result = df.Clone();

        foreach (var col in df.Columns)
        {
            if (col is not DoubleDataFrameColumn numeric)
                continue;

            var normalized = numeric.NormalizeBinsColumn(quantiles);
            result.Columns[col.Name] = new DoubleDataFrameColumn(col.Name, normalized);
        }

        return result;
    }

    // -------------------------------
    // Internal helper (column-level)
    // -------------------------------
    internal static double[] NormalizeBinsColumn(
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
            return [.. Enumerable.Repeat(0.0, (int)col.Length)];

        var weights = new double[n];
        for (int k = 0; k < n; k++)
            weights[k] = 2.0 * (bins[k] / (double)maxBin) - 1.0;

        // L1 normalisation
        var denom = weights.Sum(w => Math.Abs(w));
        if (denom <= 0)
            return [.. Enumerable.Repeat(0.0, (int)col.Length)];

        for (int k = 0; k < n; k++)
            weights[k] /= denom;

        double[] result = [.. Enumerable.Repeat(double.NaN, (int)col.Length)];
        for (int k = 0; k < n; k++)
            result[items[k].Index] = weights[k];

        return result;
    }
}