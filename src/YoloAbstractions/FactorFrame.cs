using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using MathNet.Numerics.Statistics;

namespace YoloAbstractions;

public sealed record FactorFrame(
    Matrix<double> Values, // rows = time, cols = factors or (ticker,factor)
    IReadOnlyList<DateTime> Index, // length == Values.RowCount
    IReadOnlyList<string> Columns // length == Values.ColumnCount
)
{
    public int Rows => Values.RowCount;
    public int Cols => Values.ColumnCount;

    public FactorFrame ZScoreColumns()
    {
        var m = Values.Clone();
        for (int j = 0; j < m.ColumnCount; j++)
        {
            var col = m.Column(j);
            var mean = col.Mean();
            var centered = col - Vector<double>.Build.Dense(col.Count, mean);
            var std = centered.L2Norm() / Math.Sqrt(col.Count);
            if (std == 0)
                std = 1e-12;
            m.SetColumn(j, centered / std);
        }

        return this with { Values = m };
    }
}