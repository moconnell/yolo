using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Data.Analysis;
using YoloAbstractions.Extensions;
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
        var df = new DataFrame(
        [
            new PrimitiveDataFrameColumn<DateTime>("Date", Enumerable.Repeat(timestamp, tickers.Count)),
            new StringDataFrameColumn("Ticker", tickers),
            ..values.Select(tuple => new DoubleDataFrameColumn(tuple.FactorType.ToString(), tuple.Values))
        ]);
        var factorTypes = values.Select(tuple => tuple.FactorType).ToArray();

        return new FactorDataFrame(df, factorTypes);
    }

    public double Value(FactorType factorType, string ticker) =>
        (double) _dataFrame[factorType.ToString()][_tickerIndex[ticker]];

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
        ArgumentOutOfRangeException.ThrowIfNotEqual(one.Tickers.ToCsv(), two.Tickers.ToCsv());

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

        var orderedWeights = weights.OrderBy(x => x.Key.ToString())
            .Select(x => x.Value)
            .ToArray();
        var factorCols = _dataFrame.Columns
            .Skip(2)
            .Where(c => c.Name != nameof(Volatility))
            .OrderBy(c => c.Name)
            .Cast<DoubleDataFrameColumn>()
            .ToArray();

        var rows = (int) _dataFrame.Rows.Count;
        var columns = factorCols.Length;
        var data = factorCols.Select(c => c.ToArray().Cast<double>()).ToArray();

        var m = Matrix<double>.Build.DenseOfColumns(rows, columns, data);
        var v = Vector<double>.Build.DenseOfArray(orderedWeights);

        var tickerWeights = m * v / orderedWeights.Count(w => w > 0); // (rows x 1)
        if (volatilityScaling &&
            _dataFrame.Columns.FirstOrDefault(c => c.Name == nameof(Volatility)) is DoubleDataFrameColumn volCol)
        {
            var tickerVolatilities = Vector<double>.Build.DenseOfArray(volCol.Cast<double>().ToArray());
            tickerWeights = tickerWeights.PointwiseDivide(tickerVolatilities);
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
}