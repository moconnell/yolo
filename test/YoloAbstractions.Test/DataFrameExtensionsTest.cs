using Microsoft.Data.Analysis;
using MathNet.Numerics.LinearAlgebra;
using YoloAbstractions.Extensions;
using static YoloAbstractions.NormalizationMethod;

namespace YoloAbstractions.Test;

public class DataFrameExtensionsTest
{
    [Fact]
    public void GivenNoneNormalization_WhenNormalizeCalled_ShouldReturnSameInstance()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, 2.0]));

        var result = df.Normalize(None);

        ReferenceEquals(df, result).ShouldBeTrue();
    }

    [Fact]
    public void GivenInvalidQuantiles_WhenNormalizeCalled_ShouldThrow()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, 2.0]));

        Should.Throw<ArgumentOutOfRangeException>(() => df.Normalize(CrossSectionalBins, 0));
    }

    [Fact]
    public void GivenQuantilesOneWithMissingValues_WhenNormalized_ShouldReturnNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, double.NaN, 3.0]));

        var result = df.Normalize(CrossSectionalBins, 1);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        double.IsNaN(values[0].GetValueOrDefault()).ShouldBeTrue();
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        double.IsNaN(values[2].GetValueOrDefault()).ShouldBeTrue();
    }

    [Fact]
    public void GivenAllZeroes_WhenCrossSectionalBinsNormalized_ShouldReturnNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [0.0, 0.0, 0.0, 0.0]));

        var result = df.Normalize(CrossSectionalBins, 10);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values.Length.ShouldBe(4);
        values.All(value => double.IsNaN(value.GetValueOrDefault())).ShouldBeTrue();
    }

    [Fact]
    public void GivenAllEqualWithMissingValues_WhenCrossSectionalBinsNormalized_ShouldReturnNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [5.0, double.NaN, 5.0, 5.0]));

        var result = df.Normalize(CrossSectionalBins, 10);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values.Length.ShouldBe(4);
        values.All(value => double.IsNaN(value.GetValueOrDefault())).ShouldBeTrue();
    }

    [Fact]
    public void GivenAllEqualValues_WhenCrossSectionalBinsWithDifferentQuantiles_ShouldBeIdentical()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [7.0, 7.0, 7.0, 7.0]));

        var q2 = ((DoubleDataFrameColumn)df.Normalize(CrossSectionalBins, 2)["Value"]).ToArray();
        var q10 = ((DoubleDataFrameColumn)df.Normalize(CrossSectionalBins, 10)["Value"]).ToArray();

        q2.Length.ShouldBe(q10.Length);
        for (var i = 0; i < q2.Length; i++)
        {
            double.IsNaN(q2[i].GetValueOrDefault()).ShouldBeTrue();
            double.IsNaN(q10[i].GetValueOrDefault()).ShouldBeTrue();
        }
    }

    [Fact]
    public void GivenTiedValues_WhenCrossSectionalBinsNormalized_ShouldMatchPandasQcut()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [0.0, 0.0, 0.0, 1.0, 1.0, 2.0, 2.0, 2.0]));

        var result = df.Normalize(CrossSectionalBins, 4);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values.ShouldBe([
            -0.125, -0.125, -0.125, -0.125, -0.125,
             0.125,  0.125,  0.125
        ]);
    }

    [Fact]
    public void GivenTiedValuesAndMissingValue_WhenCrossSectionalBinsNormalized_ShouldMatchPandasQcut()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, double.NaN, 1.0, 2.0, 2.0, 3.0]));

        var result = df.Normalize(CrossSectionalBins, 4);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values[0].ShouldBe(-0.2);
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        values[2].ShouldBe(-0.2);
        values[3].ShouldBe(-0.2);
        values[4].ShouldBe(-0.2);
        values[5].ShouldBe(0.2);
    }

    [Fact]
    public void GivenSeptember23FactorEnsemble_WhenRoundedAndCrossSectionalBinsNormalized_ShouldMatchPandasQcut()
    {
        // Values are the non-null 2026-09-23 factor ensemble exported by the Unravel backtest.
        var ensemble = new DoubleDataFrameColumn("Value", [
            -0.01333333333333333, 0.004444444444444441, -0.010000000000000002,
             0.03333333333333333, 0.044444444444444446, 0.0022222222222222227,
             0.002222222222222221, -0.03333333333333334, 0.014444444444444439,
             0.00444444444444445, -0.007777777777777778, 0.016666666666666663,
             0.02333333333333333, 0.014444444444444446, -0.01888888888888889,
            -0.022222222222222223, 0.03222222222222222, -0.015555555555555557,
             0.016666666666666663, 0.012222222222222223
        ]);
        double[] expected = [
            -0.055555555555555566, -0.005050505050505057, -0.04545454545454547,
             0.08585858585858587, 0.09595959595959598, -0.025252525252525262,
            -0.025252525252525262, -0.09595959595959598, 0.025252525252525252,
            -0.005050505050505057, -0.035353535353535366, 0.045454545454545456,
             0.06565656565656566, 0.025252525252525252, -0.07575757575757577,
            -0.08585858585858588, 0.07575757575757577, -0.06565656565656568,
             0.045454545454545456, 0.015151515151515159
        ];

        var actual = ((DoubleDataFrameColumn)new DataFrame(ensemble)
            .Normalize(CrossSectionalBins, 20)["Value"]).ToArray();

        actual.Length.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
            actual[i].GetValueOrDefault().ShouldBe(expected[i], tolerance: 1e-12);
    }

    [Fact]
    public void GivenCrossSectionalBins_WhenPrecisionSpecified_ShouldRoundBeforeBinning()
    {
        var values = new DoubleDataFrameColumn("Value", [0.001, 0.002, 0.003, 0.004]);

        var roundedToTwoPlaces = ((DoubleDataFrameColumn)new DataFrame(values)
            .Normalize(CrossSectionalBins, quantiles: 4, precision: 2)["Value"]).ToArray();
        var roundedToThreePlaces = ((DoubleDataFrameColumn)new DataFrame(values)
            .Normalize(CrossSectionalBins, quantiles: 4, precision: 3)["Value"]).ToArray();

        roundedToTwoPlaces.ShouldAllBe(value => double.IsNaN(value.GetValueOrDefault()));
        roundedToThreePlaces.ShouldAllBe(value => !double.IsNaN(value.GetValueOrDefault()));
    }

    [Fact]
    public void GivenRankNormalizationWithMissingValues_WhenNormalized_ShouldPreserveNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [3.0, double.NaN, 1.0]));

        var result = df.Normalize(Rank);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values[0].ShouldBe(1.0);
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        values[2].ShouldBe(-1.0);
    }

    [Fact]
    public void GivenMismatchedLengths_WhenPointwiseDivideCalled_ShouldThrow()
    {
        var col = new DoubleDataFrameColumn("Value", [1.0, 2.0]);
        var divisor = Vector<double>.Build.DenseOfArray([1.0]);

        var exception = Should.Throw<ArgumentException>(() => col.PointwiseDivide(divisor));

        exception.Message.ShouldContain("Column length");
        exception.Message.ShouldContain("divisor length");
    }

    [Fact]
    public void GivenNulls_WhenPointwiseDivideCalled_ShouldPreserveNaN()
    {
        var col = new DoubleDataFrameColumn("Value", [10.0, null, 30.0]);
        var divisor = Vector<double>.Build.DenseOfArray([2.0, 2.0, 3.0]);

        var result = col.PointwiseDivide(divisor);
        var values = result.ToArray();

        values[0].ShouldBe(5.0);
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        values[2].ShouldBe(10.0);
    }
}
