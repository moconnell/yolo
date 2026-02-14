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
    public void GivenQuantilesOneWithMissingValues_WhenNormalized_ShouldPreserveNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [1.0, double.NaN, 3.0]));

        var result = df.Normalize(CrossSectionalBins, 1);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values[0].ShouldBe(0.0);
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        values[2].ShouldBe(0.0);
    }

    [Fact]
    public void GivenAllZeroes_WhenCrossSectionalBinsNormalized_ShouldReturnAllZeroes()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [0.0, 0.0, 0.0, 0.0]));

        var result = df.Normalize(CrossSectionalBins, 10);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values.Length.ShouldBe(4);
        values[0].ShouldBe(0.0);
        values[1].ShouldBe(0.0);
        values[2].ShouldBe(0.0);
        values[3].ShouldBe(0.0);
    }

    [Fact]
    public void GivenAllEqualWithMissingValues_WhenCrossSectionalBinsNormalized_ShouldReturnZeroesAndPreserveNaN()
    {
        var df = new DataFrame(new DoubleDataFrameColumn("Value", [5.0, double.NaN, 5.0, 5.0]));

        var result = df.Normalize(CrossSectionalBins, 10);
        var values = ((DoubleDataFrameColumn)result["Value"]).ToArray();

        values.Length.ShouldBe(4);
        values[0].ShouldBe(0.0);
        double.IsNaN(values[1].GetValueOrDefault()).ShouldBeTrue();
        values[2].ShouldBe(0.0);
        values[3].ShouldBe(0.0);
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
            q2[i].ShouldBe(0.0);
            q10[i].ShouldBe(0.0);
            q2[i].ShouldBe(q10[i]);
        }
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
