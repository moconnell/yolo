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
        var tickerCol = (StringDataFrameColumn) dataFrame["Ticker"];
        var kvps = tickerCol.Select(x => KeyValuePair.Create(x, i++));
        _tickerIndex = new Dictionary<string, int>(kvps, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<FactorType> FactorTypes { get; init; }

    public IReadOnlyList<string> Tickers => ((StringDataFrameColumn) _dataFrame["Ticker"]).ToArray();
    
    public bool IsEmpty => _dataFrame.Rows.Count == 0 || FactorTypes.Count == 0 || Tickers.Count == 0;

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

    public FactorDataFrame Normalize(NormalizationMethod method = NormalizationMethod.CrossSectionalZScore)
    {
        if (method == NormalizationMethod.None)
            return this;

        var normalizedColumns = new List<DataFrameColumn>
        {
            _dataFrame["Date"],
            _dataFrame["Ticker"]
        };

        foreach (var factorType in FactorTypes.Except([Volatility]))
        {
            var colName = factorType.ToString();
            var col = (DoubleDataFrameColumn) _dataFrame[colName];

            var normalizedCol = method switch
            {
                NormalizationMethod.CrossSectionalZScore => NormalizeZScore(col),
                NormalizationMethod.MinMax => NormalizeMinMax(col),
                NormalizationMethod.Rank => NormalizeRank(col),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(method),
                    method,
                    $"Unknown normalization method: {method}")
            };

            normalizedColumns.Add(new DoubleDataFrameColumn(colName, normalizedCol));
        }

        // Add volatility column if it exists (don't normalize it)
        if (_dataFrame.Columns.FirstOrDefault(c => c.Name == nameof(Volatility)) is DoubleDataFrameColumn volCol)
        {
            normalizedColumns.Add(volCol);
        }

        var normalizedDf = new DataFrame(normalizedColumns);
        return new FactorDataFrame(normalizedDf, [..FactorTypes]);
    }

    private static IEnumerable<double> NormalizeZScore(DoubleDataFrameColumn col)
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

    private static IEnumerable<double> NormalizeMinMax(DoubleDataFrameColumn col)
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

    private static IEnumerable<double> NormalizeRank(DoubleDataFrameColumn col)
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

    public DataFrame ApplyWeights(
        IReadOnlyDictionary<FactorType, double> weights,
        double? maxWeightAbs = null,
        bool volatilityScaling = true,
        bool normalizePerAsset = true)
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

        var tickerWeightsVector = GetWeights();

        if (volatilityScaling &&
            _dataFrame.Columns.FirstOrDefault(c => c.Name == nameof(Volatility)) is DoubleDataFrameColumn volCol)
        {
            var vol = Vector<double>.Build.DenseOfArray(
                volCol.Select(x => x is > 0d ? x.Value : 1d).ToArray());
            tickerWeightsVector = tickerWeightsVector.PointwiseDivide(vol);
        }

        if (maxWeightAbs.HasValue)
        {
            tickerWeightsVector.MapInplace(x => Math.Clamp(x, -maxWeightAbs.Value, maxWeightAbs.Value));
        }

        var resultDf = new DataFrame();
        resultDf.Columns.Add(_dataFrame["Date"]);
        resultDf.Columns.Add(_dataFrame["Ticker"]);
        resultDf.Columns.Add(new DoubleDataFrameColumn("Weight", tickerWeightsVector));

        return resultDf;

        Vector<double> GetWeights()
        {
            if (normalizePerAsset && HasMissingValues())
            {
                // Calculate weight per asset, normalizing only by available factors
                var tickerWeights = new double[rows];
                for (var row = 0; row < rows; row++)
                {
                    double weightSum = 0;
                    double normalizerSum = 0;

                    for (var col = 0; col < columns; col++)
                    {
                        var value = factorCols[col][row];
                        if (value.HasValue && !double.IsNaN(value.Value))
                        {
                            weightSum += value.Value * alignedWeights[col];
                            normalizerSum += Math.Abs(alignedWeights[col]);
                        }
                    }

                    tickerWeights[row] = normalizerSum > 0 ? weightSum / normalizerSum : 0;
                }

                var tickerWeightsVector = Vector<double>.Build.DenseOfArray(tickerWeights);

                return tickerWeightsVector;
            }
            else
            {
                var data = factorCols
                    .Select(c => c.Select(x => x ?? 0d).ToArray())
                    .ToArray();

                var m = Matrix<double>.Build.DenseOfColumns(rows, columns, data);
                var v = Vector<double>.Build.DenseOfArray(alignedWeights);

                var normalizer = alignedWeights.Sum(Math.Abs);
                if (normalizer <= 0)
                    normalizer = 1;
                var tickerWeights = m * v / normalizer; // (rows x 1)

                return tickerWeights;
            }
        }

        bool HasMissingValues()
        {
            return factorCols.Any(c => c.Any(value => value is null or double.NaN));
        }
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
            double.NaN => "NaN",
            double d => d.ToString("F6"),
            DateOnly date => date.ToString("yyyy-MM-dd"),
            DateTime { Hour: 0, Minute: 0, Second: 0, Millisecond: 0, Microsecond: 0 } dt => dt.ToString("yyyy-MM-dd"),
            DateTime dt => dt.ToString("yyyy-MM-dd hh:mm:ss"),
            string s => s,
            _ => value.ToString() ?? "null"
        };
    }
}