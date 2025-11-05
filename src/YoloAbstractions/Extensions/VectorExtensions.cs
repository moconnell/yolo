using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

namespace YoloAbstractions.Extensions;

public static class VectorExtensions
{
    /// <summary>
    /// Cross-sectional transform of a single row vector (already-normalized signal):
    /// - Rank into q-tiles [0..q-1]
    /// - Map to [-1,+1] via 2*(bin/maxBin)-1
    /// - L1-normalize so sum|w|=1
    /// - If marketNeutral=false, rectifies negatives to 0 then renormalizes to sum=1 (long-only)
    /// Returns NaN for assets with NaN input.
    /// </summary>
    /// <param name="vector">Vector of daily factor values</param>
    /// <param name="qTiles">Number of bins</param>
    /// <param name="marketNeutral">If true, output should roughly sum to zero</param>
    public static Vector<double> QTiledRowToWeights(this Vector<double> vector, int qTiles, bool marketNeutral = true)
    {
        var n = vector.Count;
        var outVec = Vector<double>.Build.Dense(n, double.NaN);

        // collect valid indices
        var items = new List<(double v, int j)>(n);
        for (var j = 0; j < n; j++)
        {
            var v = vector[j];
            if (!double.IsNaN(v))
                items.Add((v, j));
        }

        if (items.Count == 0)
            return outVec;

        items.Sort((a, b) => a.v.CompareTo(b.v)); // ascending

        // assign q-tiles [0..qTiles-1]
        var m = items.Count;
        var bins = new int[m];
        for (var k = 0; k < m; k++)
        {
            var b = (int) Math.Floor((k + 1) / (double) m * qTiles) - 1;
            if (b < 0)
                b = 0;
            if (b > qTiles - 1)
                b = qTiles - 1;
            bins[k] = b;
        }

        var maxBin = bins.Max();
        if (maxBin <= 0)
            return outVec; // all equal → no signal

        // map to [-1,+1]
        var w = new double[m];
        for (var k = 0; k < m; k++)
            w[k] = 2.0 * (bins[k] / (double) maxBin) - 1.0;

        if (!marketNeutral)
        {
            // long-only: rectify, then L1 normalize to sum = 1
            for (var k = 0; k < m; k++)
                w[k] = Math.Max(0.0, w[k]);
            var sum = w.Sum();
            if (sum > 0)
                for (var k = 0; k < m; k++)
                    w[k] /= sum;
            // write back
            for (var k = 0; k < m; k++)
                outVec[items[k].j] = w[k];
            return outVec;
        }

        // market-neutral: L1 normalize so sum|w| = 1
        var denom = w.Sum(Math.Abs);
        if (denom > 0)
            for (var k = 0; k < m; k++)
                w[k] /= denom;

        for (var k = 0; k < m; k++)
            outVec[items[k].j] = w[k];

        return outVec;
    }
}