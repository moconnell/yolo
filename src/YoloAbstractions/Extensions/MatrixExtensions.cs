using System;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

namespace YoloAbstractions.Extensions;

public static class MatrixExtensions
{
    /// <summary>
    /// Returns a new matrix where each column has been z-score normalized:
    /// (x - mean) / std, computed column-wise.
    /// </summary>
    public static Matrix<double> ZScoreColumns(this Matrix<double> m)
    {
        var result = m.Clone();

        for (var j = 0; j < m.ColumnCount; j++)
        {
            var col = m.Column(j);
            var mean = col.Average();
            var std = Math.Sqrt(col.Select(x => Math.Pow(x - mean, 2)).Average());
            if (std == 0.0) std = 1e-12; // guard against divide-by-zero

            var normalized = col.Select(x => (x - mean) / std).ToArray();
            result.SetColumn(j, normalized);
        }

        return result;
    }
}