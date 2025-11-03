using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Data.Analysis;
using static YoloAbstractions.FactorType;

namespace YoloAbstractions;

public sealed record FactorDataFrame
{
    private readonly DataFrame _dataFrame;
    private readonly Dictionary<string, int> _tickerIndex;

    public FactorDataFrame(DataFrame dataFrame, params FactorType[] factorTypes)
    {
        _dataFrame = dataFrame;
        FactorTypes = factorTypes;

        var i = 0;
        var kvps = ((StringDataFrameColumn) dataFrame["Ticker"])
            .Select(x => KeyValuePair.Create(x, i++));
        _tickerIndex = new Dictionary<string, int>(kvps);
    }

    public IReadOnlyList<FactorType> FactorTypes { get; init; }

    public IReadOnlyList<string> Tickers => ((StringDataFrameColumn) _dataFrame["Ticker"]).ToArray();

    public static readonly FactorDataFrame Empty = NewFrom([], DateTime.MinValue);

    public static FactorDataFrame NewFrom(
        IReadOnlyList<string> tickers,
        DateTime timestamp,
        params (FactorType FactorType, IReadOnlyList<double> Values)[] values)
    {
        if (tickers.Select(x => x.ToUpperInvariant()).Distinct().Count() != tickers.Count)
            throw new ArgumentException("Duplicate tickers.", nameof(tickers));

        if (values.Select(v => v.FactorType).Distinct().Count() != values.Length)
            throw new ArgumentException("Duplicate factor types.", nameof(values));

        foreach (var v in values)
        {
            if (v.Values.Count != tickers.Count)
                throw new ArgumentException(
                    $"Length mismatch for {v.FactorType}: expected {tickers.Count}, got {v.Values.Count}.");
        }

        var df = new DataFrame(
        [
            new PrimitiveDataFrameColumn<DateTime>("Date", Enumerable.Repeat(timestamp, tickers.Count)),
            new StringDataFrameColumn("Ticker", tickers),
            ..values.Select(tuple => new DoubleDataFrameColumn(tuple.FactorType.ToString(), tuple.Values))
        ]);
        var factorTypes = values.Select(tuple => tuple.FactorType).ToArray();

        return new FactorDataFrame(df, factorTypes);
    }

    public double this[FactorType factorType, string ticker]
    {
        get
        {
            if (!FactorTypes.Contains(factorType) || !_tickerIndex.TryGetValue(ticker, out var index))
            {
                return double.NaN;
            }

            var val = (double?) _dataFrame[factorType.ToString()][index];
            return val ?? double.NaN;
        }
    }

    // Indexer by ticker symbol
    public IReadOnlyDictionary<FactorType, double> this[string ticker]
    {
        get
        {
            // locate the matching row(s)
            if (!_tickerIndex.TryGetValue(ticker, out var rowIndex))
                throw new KeyNotFoundException($"{ticker} does not exist");

            // build a dictionary FactorType -> value
            var dict = new Dictionary<FactorType, double>();

            foreach (var factorType in FactorTypes)
            {
                var col = (DoubleDataFrameColumn) _dataFrame[factorType.ToString()];
                dict[factorType] = col[rowIndex] ?? double.NaN;
            }

            return dict;
        }
    }

    public static FactorDataFrame operator +(FactorDataFrame one, FactorDataFrame two)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(two);

        if (one.Tickers.Count != two.Tickers.Count ||
            one.Tickers.Except(two.Tickers, StringComparer.OrdinalIgnoreCase).Any())
            throw new ArgumentException("Ticker sets must match.");

        var sharedCols = one._dataFrame.Columns
            .Select(c => c.Name)
            .Intersect(two._dataFrame.Columns.Select(c => c.Name))
            .Where(name => !string.Equals(name, "Ticker", StringComparison.Ordinal) &&
                           !string.Equals(name, "Date", StringComparison.Ordinal))
            .ToArray();
        if (sharedCols.Length != 0)
            throw new ArgumentException(
                $"Cannot merge DataFrames with overlapping Factor columns: {string.Join(", ", sharedCols)}");

        var joinedColumns = one._dataFrame.Columns
            .UnionBy(two._dataFrame.Columns, c => c.Name)
            .ToArray();
        var df = new DataFrame(joinedColumns);
        var joinedFactorTypes = one.FactorTypes.Union(two.FactorTypes).Distinct().ToArray();

        return new FactorDataFrame(df, joinedFactorTypes);
    }

    public DataFrame ApplyWeights(
        IReadOnlyDictionary<FactorType, double> weights,
        double? maxWeightAbs = null,
        bool volatilityScaling = true)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var factorCols = _dataFrame.Columns
            .Skip(2)
            .Where(c => !string.Equals(c.Name, nameof(Volatility), StringComparison.Ordinal))
            .OfType<DoubleDataFrameColumn>()
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToArray();
        var alignedWeights = factorCols
            .Select(c => Enum.TryParse<FactorType>(c.Name, out var ft) && weights.TryGetValue(ft, out var w) ? w : 0d)
            .ToArray();

        var rows = (int) _dataFrame.Rows.Count;
        var columns = factorCols.Length;
        var data = factorCols
            .Select(c => c.Select(x => x ?? 0d).ToArray())
            .ToArray();

        var m = Matrix<double>.Build.DenseOfColumns(rows, columns, data);
        var v = Vector<double>.Build.DenseOfArray(alignedWeights);

        var normalizer = alignedWeights.Sum(Math.Abs);
        if (normalizer <= 0)
            normalizer = 1;
        var tickerWeights = m * v / normalizer; // (rows x 1)
        if (volatilityScaling &&
            _dataFrame.Columns.FirstOrDefault(c => c.Name == nameof(Volatility)) is DoubleDataFrameColumn volCol)
        {
            var vol = Vector<double>.Build.DenseOfArray(
                volCol.Select(x => x is > 0d ? x.Value : 1d).ToArray());
            tickerWeights = tickerWeights.PointwiseDivide(vol);
        }

        if (maxWeightAbs.HasValue)
        {
            tickerWeights.MapInplace(x => Math.Clamp(x, -maxWeightAbs.Value, maxWeightAbs.Value));
        }

        var resultDf = new DataFrame();
        resultDf.Columns.Add(_dataFrame["Date"]);
        resultDf.Columns.Add(_dataFrame["Ticker"]);
        resultDf.Columns.Add(new DoubleDataFrameColumn("Weight", tickerWeights));

        return resultDf;
    }


    public override string ToString()
    {
        if (_dataFrame.Rows.Count == 0)
            return "Empty FactorDataFrame";

        var sb = new System.Text.StringBuilder();
        var columns = _dataFrame.Columns.ToList();
        var rowCount = (int) _dataFrame.Rows.Count;

        // Calculate column widths
        var columnWidths = new int[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            columnWidths[i] = col.Name.Length;

            for (var row = 0; row < rowCount; row++)
            {
                var value = FormatValue(col[row]);
                columnWidths[i] = Math.Max(columnWidths[i], value.Length);
            }
        }

        // Add row index width
        var indexWidth = Math.Max(rowCount.ToString().Length, 0) + 2;

        // Header row
        sb.Append(new string(' ', indexWidth));
        for (var i = 0; i < columns.Count; i++)
        {
            sb.Append(columns[i].Name.PadLeft(columnWidths[i] + 2));
        }

        sb.AppendLine();

        // Separator line
        sb.Append(new string(' ', indexWidth));
        for (var i = 0; i < columns.Count; i++)
        {
            sb.Append(new string('-', columnWidths[i] + 2));
        }

        sb.AppendLine();

        // Data rows
        for (var row = 0; row < rowCount; row++)
        {
            // Row index
            sb.Append(row.ToString().PadLeft(indexWidth));

            // Column values
            for (var col = 0; col < columns.Count; col++)
            {
                var value = FormatValue(columns[col][row]);
                sb.Append(value.PadLeft(columnWidths[col] + 2));
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"[{rowCount} rows x {columns.Count} columns]");

        return sb.ToString();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "NaN",
            double d when double.IsNaN(d) => "NaN",
            double d => d.ToString("F6"),
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            string s => s,
            _ => value.ToString() ?? "null"
        };
    }
}